using System.Collections.Generic;
using System.Linq;

namespace KDCVRCBSP.ECL {
	/// A set of faces with windings and associated data.
	/// (Usually, the associated data would be a brush side.)
	/// This class is immutable, and so are its faces.
	public sealed class Convex3d<D> {
		/// A bounded convex shape in 3D cannot have less than this amount of faces.
		public const int ConvexCollapseLimit = 4;

		/// How many points a winding needs.
		public const int WindingCollapseLimit = 3;

		public readonly Geo2Context g2;
		public readonly IReadOnlyList<Face> faces;
		public readonly AABB3d bounds;

		public Convex3d(Geo2Context g2, IReadOnlyList<Face> faces) {
			this.g2 = g2;
			this.faces = faces;
			AABB3d boundsAdj = new AABB3d {
				min = Vector3d.Zero,
				max = Vector3d.Zero
			};
			bool first = true;
			foreach (Face f in faces) {
				if (first) {
					boundsAdj = f.bounds;
					first = false;
				} else {
					boundsAdj = boundsAdj.Join(f.bounds);
				}
			}
			bounds = boundsAdj;
		}

		/// Convex face.
		/// This can be used with or without the parent convex.
		public sealed class Face {
			public readonly Geo2Context g2;
			public readonly int planeIndex;
			public readonly IReadOnlyList<Vector3d> winding;
			public readonly D data;
			public readonly AABB3d bounds;

			public Face(Geo2Context g2, int planeIndex, IReadOnlyList<Vector3d> winding, D data) {
				this.g2 = g2;
				this.planeIndex = planeIndex;
				this.winding = winding;
				this.bounds = new AABB3d(winding);
				this.data = data;
			}

			/// Cuts this face in two by a plane.
			/// Faces which did not survive as faces at all are returned as null.
			/// Returns (negative, positive).
			public (Face, Face) Cut(Plane3d cut) {
				List<Vector3d> neg = new(this.winding);
				List<Vector3d> pos = new();
				cut.CutWinding(neg, pos, g2.distanceEpsilon);
				return (
					neg.Count >= WindingCollapseLimit ? new Face(g2, planeIndex, neg, data) : null,
					pos.Count >= WindingCollapseLimit ? new Face(g2, planeIndex, pos, data) : null
				);
			}
		}

		/// Creates a convex out of planes.
		/// Returns null if the brush has less than the minimum amount of faces to be a solid (ConvexCollapseLimit)
		public static Convex3d<D> FromPlanes(Geo2Context g2, Plane3d[] planes, D[] associated) {
			List<Face> faces = new();
			for (int i = 0; i < planes.Length; i++) {
				var winding = GeomUtil.GenInitialWinding(planes[i], g2.initialWindingSize);
				for (int j = 0; j < planes.Length; j++) {
					if (i == j)
						continue;
					planes[j].CutWinding(winding, null, g2.distanceEpsilon);
					if (winding.Count < WindingCollapseLimit)
						break;
				}
				if (winding.Count >= WindingCollapseLimit)
					faces.Add(new Face(g2, g2.ToPlaneIndex(planes[i]), winding, associated[i]));
			}
			if (faces.Count < ConvexCollapseLimit)
				return null;
			return new Convex3d<D>(g2, faces);
		}

		/// Performs the BSP 'chop' stage to create a list of chopped faces.
		/// The convex list is assumed to contain the current brush.
		/// Notably, this may return the original face list.
		public IReadOnlyList<Face> ChopFaces(IReadOnlyList<Convex3d<D>> allBrushes) {
			bool afterSelf = false;
			IReadOnlyList<Face> oldFaces = faces;
			// begin chopping
			foreach (Convex3d<D> cutterBrush in allBrushes) {
				if (cutterBrush == this) {
					afterSelf = true;
					continue;
				}
				// N^2 algorithm moment. Such is life.
				if (!cutterBrush.bounds.Intersects(bounds, g2.broadphaseEpsilon))
					continue;
				// Cut up each of our faces.
				List<Face> newFaces = new();
				foreach (Face oldFace in oldFaces) {
					// First, create intersection winding.
					List<Vector3d> intersectionWinding = new(oldFace.winding);
					// If the cutter brush has a face coplanar with ours, we need to know this.
					// Mostly, we're doing a subtraction operation.
					// But for a coplanar face we will keep the intersection of the 'winner'.
					bool overlapped = false;
					List<List<Vector3d>> debugDoBadDirectCut = g2.debugChopFacesDoBadDirectCut ? new() : null;
					int oldFacePlaneIndexFlipped = g2.FlipPlaneIndex(oldFace.planeIndex);
					foreach (Face cutterFace in cutterBrush.faces) {
						if (cutterFace.planeIndex == oldFace.planeIndex) {
							// Note we set this only if they face the same way.
							// If they face opposite ways, they're supposed to cancel out.
							overlapped = true;
							continue;
						} else if (cutterFace.planeIndex == oldFacePlaneIndexFlipped) {
							continue;
						}
						var cutterPlane = g2.FromPlaneIndex(cutterFace.planeIndex);
						List<Vector3d> subWinding = debugDoBadDirectCut != null ? new() : null;
						cutterPlane.CutWinding(intersectionWinding, subWinding, g2.distanceEpsilon);
						if (subWinding != null)
							debugDoBadDirectCut.Add(subWinding);
						if (intersectionWinding.Count < WindingCollapseLimit)
							break;
					}
					if (intersectionWinding.Count < WindingCollapseLimit) {
						// Does not intersect at all.
						newFaces.Add(oldFace);
						continue;
					}
					if (debugDoBadDirectCut != null) {
						// For debugging issues, we can use this method instead.
						// This can cause excessive cutting from unrelated faces we can't easily prove don't intersect.
						foreach (var flakeWinding in debugDoBadDirectCut)
							if (flakeWinding.Count >= WindingCollapseLimit)
								newFaces.Add(new Face(g2, oldFace.planeIndex, flakeWinding, oldFace.data));
					} else {
						// Ok, so now we have the fun bit. This face DEFINITELY intersects the cutter brush.
						// To avoid generating unnecessary splits from random other geometry,
						//  we need to make a series of cut planes from the winding.
						Plane3d oldFacePlane = g2.FromPlaneIndex(oldFace.planeIndex);
						List<Plane3d> cutPlanes = GeomUtil.WindingToPlanes(intersectionWinding, oldFacePlane.normal);
						List<Vector3d> remainderWinding = new(oldFace.winding);
						foreach (var plane in cutPlanes) {
							// Below the plane gets cut up into basically a copy of intersectionWinding.
							List<Vector3d> flakeWinding = new();
							plane.CutWinding(remainderWinding, flakeWinding, g2.distanceEpsilon);
							if (flakeWinding.Count >= WindingCollapseLimit)
								newFaces.Add(new Face(g2, oldFace.planeIndex, flakeWinding, oldFace.data));
							if (remainderWinding.Count < WindingCollapseLimit)
								break;
						}
					}
					// Overlapped 'winner' logic.
					if (overlapped && afterSelf)
						newFaces.Add(new Face(g2, oldFace.planeIndex, intersectionWinding, oldFace.data));
				}
				oldFaces = newFaces;
			}
			return oldFaces;
		}
	}
}