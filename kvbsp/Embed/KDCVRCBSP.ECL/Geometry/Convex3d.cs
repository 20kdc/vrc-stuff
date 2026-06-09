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

		/// Cuts a convex to create up to two new convexes.
		public (Convex3d<D>, Convex3d<D>) Cut(Plane3d plane, D planeData) {
			var winding = GeomUtil.GenInitialWinding(plane, initialWindingSize);
			List<Face> facesBelow = new();
			List<Face> facesAbove = new();
			foreach (var face in this.faces) {
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
				facesAbove.Add(new Face(plane.Flipped, windingRev, planeData));
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

		/// Creates a convex out of planes.
		/// Returns null if the brush has less than the minimum amount of faces to be a solid (ConvexCollapseLimit)
		/// Unless acceptUnbounded is true.
		public static Convex3d<D> FromPlanes(Plane3d[] planes, D[] associated, double distanceEpsilon, double initialWindingSize, bool acceptUnbounded = false) {
			List<Face> faces = new();
			for (int i = 0; i < planes.Length; i++) {
				var winding = GeomUtil.GenInitialWinding(planes[i], initialWindingSize);
				for (int j = 0; j < planes.Length; j++) {
					if (i == j)
						continue;
					planes[j].CutWinding(winding, null, distanceEpsilon);
					if (winding.Count < WindingCollapseLimit)
						break;
				}
				if (winding.Count >= WindingCollapseLimit)
					faces.Add(new Face(planes[i], winding, associated[i]));
			}
			if ((faces.Count < ConvexCollapseLimit) && !acceptUnbounded)
				return null;
			return new Convex3d<D>(faces, distanceEpsilon, initialWindingSize);
		}
	}
}
