using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace KDCVRCBSP.ECL {
	/// Represents a parsed map entity.
	/// A parsed map is simply a list of entities, so it's a big deal that we get these right.
	public sealed class EntityParsed<M> {
		/// List of key/value pairs.
		public EntityKeys pairs = new();

		/// List of brushes as lists of brush sides.
		/// A brush is the fundamental geometric primitive, representing a convex object.
		public List<List<BrushSide>> brushes = new();

		/// Ensures worldspawn exists, creating it if it doesn't.
		public static EntityParsed<M> EnsureWorldspawn(List<EntityParsed<M>> entities) {
			foreach (var ep in entities)
				if (ep.pairs["classname"] == "worldspawn")
					return ep;
			var worldspawn = new EntityParsed<M>();
			worldspawn.pairs["classname"] = "worldspawn";
			entities.Insert(0, worldspawn);
			return worldspawn;
		}

		/// Pre-compiles brush planes for efficiency.
		public static Plane3d[] BrushPlanes(IList<BrushSide> src) {
			Plane3d[] res = new Plane3d[src.Count];
			for (int i = 0; i < res.Length; i++)
				res[i] = src[i].Plane;
			return res;
		}

		/// A brush side represents a side of a brush.
		/// A brush is a list of brush sides.
		public sealed class BrushSide {
			/// Texture representation.
			public M texture;

			/// Source vertices (for precision)
			/// Observed TB behaviour is to carry these over.
			public Vector3d vertexA, vertexB, vertexC;

			/// Plane.
			public Plane3d Plane {
				[MethodImpl(MethodImplOptions.AggressiveInlining)]
				get => new Plane3d(vertexA, vertexB, vertexC);
			}

			public BrushUV texUV;

			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public Vector2d MapUV(Vector3d i) => texUV.MapUV(i);

			/// Translates the brush side in 3D space.
			public BrushSide Translated(Vector3d by) {
				return new BrushSide {
					texture = texture,
					vertexA = vertexA + by,
					vertexB = vertexB + by,
					vertexC = vertexC + by,
					texUV = texUV.Translated(by)
				};
			}
		}
	}
}
