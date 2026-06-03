using System;
using System.Collections.Generic;

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

		/// Creates a Convex3d from a list of brush sides.
		/// Returns null if the brush has less than the minimum amount of faces to be a solid (ConvexCollapseLimit)
		public static Convex3d<D> FromBrush<M>(Geo2Context g2, IReadOnlyList<EntityParsed<M>.BrushSide> src, Func<EntityParsed<M>.BrushSide, D> map) {
			Plane3d[] planes = new Plane3d[src.Count];
			D[] datas = new D[src.Count];
			for (int i = 0; i < planes.Length; i++) {
				planes[i] = src[i].Plane;
				datas[i] = map(src[i]);
			}
			return FromPlanes(g2, planes, datas);
		}

		/// Remaps data.
		public Convex3d<E> MapData<E>(Func<D, E> fn) {
			List<Convex3d<E>.Face> newFaces = new();
			foreach (Face f in faces)
				newFaces.Add(f.WithData(fn(f.data)));
			return new Convex3d<E>(g2, newFaces);
		}

		/// Cuts a convex to create up to two new convexes.
		public (Convex3d<D>, Convex3d<D>) Cut(int planeIdx, D planeData) {
			var plane = g2.FromPlaneIndex(planeIdx);
			var planeFlipIdx = g2.FlipPlaneIndex(planeIdx);
			var winding = GeomUtil.GenInitialWinding(plane, g2.initialWindingSize);
			List<Face> facesBelow = new();
			List<Face> facesAbove = new();
			foreach (var face in this.faces) {
				g2.FromPlaneIndex(face.planeIndex).CutWinding(winding, null, g2.distanceEpsilon);
				(var faceBelow, var faceAbove) = face.Cut(plane);
				if (faceBelow != null)
					facesBelow.Add(faceBelow);
				if (faceAbove != null)
					facesAbove.Add(faceAbove);
			}
			// If the winding survived, include it and its copy.
			if (winding.Count >= WindingCollapseLimit) {
				facesBelow.Add(new Face(g2, planeIdx, winding, planeData));
				List<Vector3d> windingRev = new(winding);
				windingRev.Reverse();
				facesAbove.Add(new Face(g2, planeFlipIdx, windingRev, planeData));
			}
			return (
				(facesBelow.Count < ConvexCollapseLimit) ? null : new Convex3d<D>(g2, facesBelow),
				(facesAbove.Count < ConvexCollapseLimit) ? null : new Convex3d<D>(g2, facesAbove)
			);
		}

		/// Signed distance to convex.
		/// Think like signed distance fields.
		/// Or clouds! We all like clouds, right?
		public double SignedDistance(Vector3d pos) {
			double v = double.NegativeInfinity;
			foreach (var face in faces)
				v = Math.Max(v, g2.FromPlaneIndex(face.planeIndex).SignedDistance(pos));
			return v;
		}

		/// Convex face.
		/// This can be used with or without the parent convex.
		public sealed class Face {
			public readonly Geo2Context g2;
			public readonly int planeIndex;
			public readonly IReadOnlyList<Vector3d> winding;
			public readonly D data;
			public readonly AABB3d bounds;

			private Face(Geo2Context g2, int planeIndex, IReadOnlyList<Vector3d> winding, D data, AABB3d bounds) {
				this.g2 = g2;
				this.planeIndex = planeIndex;
				this.winding = winding;
				this.data = data;
				this.bounds = bounds;
			}

			public Face(Geo2Context g2, int planeIndex, IReadOnlyList<Vector3d> winding, D data) {
				this.g2 = g2;
				this.planeIndex = planeIndex;
				this.winding = winding;
				this.data = data;
				this.bounds = new AABB3d(winding);
			}

			public Convex3d<E>.Face WithData<E>(E newData) {
				return new(g2, planeIndex, winding, newData, bounds);
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
		/// Unless acceptUnbounded is true.
		public static Convex3d<D> FromPlanes(Geo2Context g2, Plane3d[] planes, D[] associated, bool acceptUnbounded = false) {
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
			if ((faces.Count < ConvexCollapseLimit) && !acceptUnbounded)
				return null;
			return new Convex3d<D>(g2, faces);
		}

		/// Performs the BSP 'chop' stage to create a list of chopped faces.
		/// The convex list may contain this brush.
		/// In this event, this brush takes priority over brushes that follow it in overlap handling.
		/// It's an erroneous contradiction for a brush to take priority but then not split other faces.
		/// So all such brushes are, philosophically, 'at the end of the list'.
		/// Notably, this function may return the original face list if no chopping occurs.
		public IReadOnlyList<Face> ChopFaces(IReadOnlyList<Convex3d<D>> allBrushes, Func<Face, BSPSurfaceFlags> getChopFlags) {
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
				// Assemble list of chopper faces.
				List<(Face, bool)> cutterFaces = new();
				foreach (Face f in cutterBrush.faces)
					cutterFaces.Add((f, (getChopFlags(f) & BSPSurfaceFlags.NoChopOthers) == 0));
				// Cut up each of our faces.
				List<Face> newFaces = new();
				bool wasAnyFaceChoppable = false;
				foreach (Face oldFace in oldFaces) {
					BSPSurfaceFlags cf = getChopFlags(oldFace);
					// Unchoppable faces can't be chopped. (Crazy, right~? -A)
					if ((cf & BSPSurfaceFlags.NoChopThis) != 0) {
						newFaces.Add(oldFace);
						continue;
					}
					wasAnyFaceChoppable = true;
					ChopFace(g2, newFaces, oldFace, cutterFaces, afterSelf);
				}
				// if we never had any choppable faces in the first place, we're never going to.
				if (!wasAnyFaceChoppable)
					return oldFaces;
				oldFaces = newFaces;
			}
			return oldFaces;
		}

		/// Chops a face.
		public static void ChopFace(Geo2Context g2, List<Face> dest, Face oldFace, IReadOnlyList<(Face, bool)> cutterFaces, bool cutterAfterSelf) {
			// First, create intersection winding.
			// The booleans here determine which cuts we're actually allowed to make proper.
			// If we're allowed to make all cuts in the intersection, then we're allowed to 'punch out' the face.
			// This is when the presence of the intersection entirely removes the geometry, and is the primary goal of the chop stage.
			List<(Vector3d, bool)> intersectionWinding = new();
			// We permit cutting by faces that come from the original face.
			// This will do nothing and is needed to allow punch-out from faces on an edge.
			foreach (var v in oldFace.winding)
				intersectionWinding.Add((v, true));
			// If the cutter brush has a face coplanar with ours, we need to know this.
			// Mostly, we're doing a subtraction operation.
			// But for a coplanar face we will keep the intersection of the 'winner'.
			bool overlapped = false;
			int oldFacePlaneIndexFlipped = g2.FlipPlaneIndex(oldFace.planeIndex);
			foreach ((var cutterFace, bool canChop) in cutterFaces) {
				if (cutterFace.planeIndex == oldFace.planeIndex) {
					// Note we set this only if they face the same way.
					// If they face opposite ways, they're supposed to cancel out.
					overlapped = true;
					continue;
				} else if (cutterFace.planeIndex == oldFacePlaneIndexFlipped) {
					continue;
				}
				var cutterPlane = g2.FromPlaneIndex(cutterFace.planeIndex);
				// Be 'optimistic' about on-plane lines here.
				// The overlapping face conundrum is covered because we don't actually cut using those faces anyway.
				cutterPlane.CutWindingMD(intersectionWinding, null, g2.distanceEpsilon, (a, b) => a || b, canChop);
				if (intersectionWinding.Count < WindingCollapseLimit)
					break;
			}
			if (intersectionWinding.Count < WindingCollapseLimit) {
				// Does not intersect at all.
				dest.Add(oldFace);
				return;
			}
			// Ok, so now we have the fun bit. This face DEFINITELY intersects the cutter brush.
			// To avoid generating unnecessary splits from random other geometry,
			//  we need to make a series of cut planes from the winding.
			Plane3d oldFacePlane = g2.FromPlaneIndex(oldFace.planeIndex);
			List<(Plane3d, bool)> cutPlanes = Plane3d.WindingToPlanesMD<bool>(intersectionWinding, oldFacePlane.normal);
			List<Vector3d> remainderWinding = new(oldFace.winding);
			bool cutIncomplete = false;
			foreach (var plane in cutPlanes) {
				if (!plane.Item2) {
					cutIncomplete = true;
					continue;
				}
				if (remainderWinding.Count < WindingCollapseLimit)
					continue;
				// Below the plane gets cut up into basically a copy of intersectionWinding.
				List<Vector3d> flakeWinding = new();
				plane.Item1.CutWinding(remainderWinding, flakeWinding, g2.distanceEpsilon);
				if (flakeWinding.Count >= WindingCollapseLimit)
					dest.Add(new Face(g2, oldFace.planeIndex, flakeWinding, oldFace.data));
			}
			// This logic covers these cases:
			// 1. We're overlapping the cutter brush and we supersede the cutter brush.
			// 2. Not all intersection lines are allowed to chop, so we *can't* safely punch-out this face.
			if ((overlapped && cutterAfterSelf) || cutIncomplete)
				dest.Add(new Face(g2, oldFace.planeIndex, remainderWinding, oldFace.data));
		}
	}
}
