using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace KDCVRCBSP.ECL {
	/// A set of faces with windings and associated data.
	/// (Usually, the associated data would be a brush side.)
	/// This class is immutable, and so are its faces.
	public sealed class Convex3d<D> {
		/// A bounded convex shape in 3D cannot have less than this amount of faces.
		public const int ConvexCollapseLimit = 4;

		/// How many points a winding needs.
		public const int WindingCollapseLimit = 3;

		public readonly IReadOnlyList<Face> faces;
		public readonly AABB3d bounds;
		public readonly double distanceEpsilon, initialWindingSize;

		public Convex3d(IReadOnlyList<Face> faces, double distanceEpsilon, double initialWindingSize) {
			this.faces = faces;
			this.distanceEpsilon = distanceEpsilon;
			this.initialWindingSize = initialWindingSize;
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

		/// Determines if this convex is unclosed by if the bounds are too close to the edge.
		public bool IsUnclosed {
			get {
				var effectiveInfinity = initialWindingSize - distanceEpsilon;
				var effectiveInfinityN = -effectiveInfinity;
				if (bounds.min.x < effectiveInfinityN)
					return true;
				if (bounds.min.y < effectiveInfinityN)
					return true;
				if (bounds.min.z < effectiveInfinityN)
					return true;
				if (bounds.max.x > effectiveInfinity)
					return true;
				if (bounds.max.y > effectiveInfinity)
					return true;
				if (bounds.max.z > effectiveInfinity)
					return true;
				return false;
			}
		}

		/// Remaps data.
		public Convex3d<E> MapData<E>(Func<D, E> fn) {
			List<Convex3d<E>.Face> newFaces = new();
			foreach (Face f in faces)
				newFaces.Add(f.WithData(fn(f.data)));
			return new Convex3d<E>(newFaces, distanceEpsilon, initialWindingSize);
		}

		public Convex3d<D> SubstituteFaceData(Face fSrc, D newData) {
			List<Convex3d<D>.Face> newFaces = new();
			foreach (Face f in faces) {
				if (f == fSrc) {
					newFaces.Add(f.WithData(newData));
				} else {
					newFaces.Add(f);
				}
			}
			return new Convex3d<D>(newFaces, distanceEpsilon, initialWindingSize);
		}

		/// Cuts a convex to create up to two new convexes.
		public (Convex3d<D>, Convex3d<D>) Cut(Plane3d plane, D planeData) {
			var planeFlipped = plane.Flipped;
			var winding = GeomUtil.GenInitialWinding(plane, initialWindingSize);
			List<Face> facesBelow = new();
			List<Face> facesAbove = new();
			foreach (var face in this.faces) {
				if (face.plane.Near(plane, distanceEpsilon)) {
					// must be entirely below
					return (this.SubstituteFaceData(face, planeData), null);
				} else if (face.plane.Near(plane, distanceEpsilon)) {
					// must be entirely above
					return (null, this.SubstituteFaceData(face, planeData));
				}
				face.plane.CutWinding(winding, null, distanceEpsilon);
				(var faceBelow, var faceAbove) = face.Cut(plane, distanceEpsilon);
				if (faceBelow != null)
					facesBelow.Add(faceBelow);
				if (faceAbove != null)
					facesAbove.Add(faceAbove);
			}
			// If the winding survived, include it and its copy.
			if (winding.Count >= WindingCollapseLimit) {
				facesBelow.Add(new Face(plane, winding, planeData));
				List<Vector3d> windingRev = new(winding);
				windingRev.Reverse();
				facesAbove.Add(new Face(planeFlipped, windingRev, planeData));
			}
			return (
				(facesBelow.Count < ConvexCollapseLimit) ? null : new Convex3d<D>(facesBelow, distanceEpsilon, initialWindingSize),
				(facesAbove.Count < ConvexCollapseLimit) ? null : new Convex3d<D>(facesAbove, distanceEpsilon, initialWindingSize)
			);
		}

		/// Signed distance to convex.
		/// Think like signed distance fields.
		/// Or clouds! We all like clouds, right?
		public double SignedDistance(Vector3d pos) {
			double v = double.NegativeInfinity;
			foreach (var face in faces)
				v = Math.Max(v, face.plane.SignedDistance(pos));
			return v;
		}

		/// Convex face.
		/// This can be used with or without the parent convex.
		public sealed class Face {
			public readonly Plane3d plane;
			public readonly IReadOnlyList<Vector3d> winding;
			public readonly D data;
			public readonly AABB3d bounds;

			private Face(Plane3d plane, IReadOnlyList<Vector3d> winding, D data, AABB3d bounds) {
				this.plane = plane;
				this.winding = winding;
				this.data = data;
				this.bounds = bounds;
			}

			public Face(Plane3d plane, IReadOnlyList<Vector3d> winding, D data) {
				this.plane = plane;
				this.winding = winding;
				this.data = data;
				this.bounds = new AABB3d(winding);
			}

			public Convex3d<E>.Face WithData<E>(E newData) {
				return new(plane, winding, newData, bounds);
			}

			/// Cuts this face in two by a plane.
			/// Faces which did not survive as faces at all are returned as null.
			/// Returns (negative, positive).
			public (Face, Face) Cut(Plane3d cut, double distanceEpsilon) {
				List<Vector3d> neg = new(this.winding);
				List<Vector3d> pos = new();
				cut.CutWinding(neg, pos, distanceEpsilon);
				return (
					neg.Count >= WindingCollapseLimit ? new Face(plane, neg, data) : null,
					pos.Count >= WindingCollapseLimit ? new Face(plane, pos, data) : null
				);
			}
		}

		/// Same as the other FromPlanes, but sets up a filled array automatically for simple cases.
		public static Convex3d<D> FromPlanes(Plane3d[] planes, D associated, double distanceEpsilon, double initialWindingSize, bool acceptUnbounded = false) {
			D[] arr = new D[planes.Length];
			for (int i = 0; i < planes.Length; i++)
				arr[i] = associated;
			return FromPlanes(planes, arr, distanceEpsilon, initialWindingSize, acceptUnbounded);
		}

		/// Creates a convex out of planes.
		/// Returns null if the brush has less than the minimum amount of faces to be a solid (ConvexCollapseLimit)...
		/// Unless acceptUnbounded is true (since unbounded convexes can easily have this).
		public static Convex3d<D> FromPlanes(Plane3d[] planes, D[] associated, double distanceEpsilon, double initialWindingSize, bool acceptUnbounded = false) {
			List<Face> faces = new();
			for (int i = 0; i < planes.Length; i++) {
				var winding = GeomUtil.GenInitialWinding(planes[i], initialWindingSize);
				bool fault = false;
				for (int j = 0; j < planes.Length; j++) {
					if (i == j)
						continue;
					if (j < i) {
						// if coplanarity happens, existing plane takes priority
						if (planes[i].Near(planes[j], distanceEpsilon)) {
							fault = true;
							break;
						}
					}
					planes[j].CutWinding(winding, null, distanceEpsilon);
					if (winding.Count < WindingCollapseLimit) {
						fault = true;
						break;
					}
				}
				if (fault)
					continue;
				faces.Add(new Face(planes[i], winding, associated[i]));
			}
			if ((faces.Count < ConvexCollapseLimit) && !acceptUnbounded)
				return null;
			var convex = new Convex3d<D>(faces, distanceEpsilon, initialWindingSize);
			if (convex.IsUnclosed && !acceptUnbounded)
				return null;
			return convex;
		}
	}
}
