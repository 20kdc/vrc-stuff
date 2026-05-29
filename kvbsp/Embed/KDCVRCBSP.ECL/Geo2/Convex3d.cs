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

		public readonly IReadOnlyList<Face> faces;
		public readonly AABB3d bounds;

		public Convex3d(IReadOnlyList<Face> faces) {
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
			public readonly Plane3d plane;
			public readonly IReadOnlyList<Vector3d> winding;
			public readonly D data;
			public readonly AABB3d bounds;

			public Face(Plane3d plane, IReadOnlyList<Vector3d> winding, D data) {
				this.plane = plane;
				this.winding = winding;
				this.bounds = new AABB3d(winding);
				this.data = data;
			}

			/// Cuts this face in two by a plane.
			/// Faces which did not survive as faces at all are returned as null.
			/// Returns (negative, positive).
			public (Face, Face) Cut(Plane3d cut, double epsilon) {
				List<Vector3d> neg = new(this.winding);
				List<Vector3d> pos = new();
				cut.CutWinding(neg, pos, epsilon);
				return (
					neg.Count >= WindingCollapseLimit ? new Face(plane, neg, data) : null,
					pos.Count >= WindingCollapseLimit ? new Face(plane, pos, data) : null
				);
			}
		}

		/// Creates a convex out of planes.
		/// Returns null if the brush has less than the minimum amount of faces to be a solid (ConvexCollapseLimit)
		public static Convex3d<D> FromPlanes(Plane3d[] planes, D[] associated, double epsilon, double initialWinding) {
			List<Face> faces = new();
			for (int i = 0; i < planes.Length; i++) {
				var winding = GeomUtil.GenInitialWinding(planes[i], initialWinding);
				for (int j = 0; j < planes.Length; j++) {
					if (i == j)
						continue;
					planes[j].CutWinding(winding, null, epsilon);
					if (winding.Count < WindingCollapseLimit)
						break;
				}
				if (winding.Count >= WindingCollapseLimit)
					faces.Add(new Face(planes[i], winding, associated[i]));
			}
			if (faces.Count < ConvexCollapseLimit)
				return null;
			return new Convex3d<D>(faces);
		}

		/* NOT YET FINISHED! Need to finish work on Geo2
		/// Performs the BSP 'chop' stage to create a list of chopped faces.
		/// The convex list is assumed to contain the current brush.
		/// Notably, this may return the original face list.
		public IReadOnlyList<Face> ChopFaces(IReadOnlyList<Convex3d<D>> allBrushes) {
			bool afterSelf = false;
			// begin chopping
			foreach (Convex3d<D> brush in allBrushes) {
				if (brush == this) {
					afterSelf = true;
					continue;
				}
				List<Convex3d<EntityParsed.BrushSide>.Face> newFaces = new();
				faces = newFaces;
			}
		}
		*/
	}
}