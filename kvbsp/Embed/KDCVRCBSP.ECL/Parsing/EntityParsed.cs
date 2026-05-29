using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace KDCVRCBSP.ECL {
	/// Represents a parsed map entity.
	/// A parsed map is simply a list of entities, so it's a big deal that we get these right.
	public sealed class EntityParsed {
		/// List of key/value pairs.
		public List<(string, string)> pairs = new();

		/// List of brushes as lists of brush sides.
		/// A brush is the fundamental geometric primitive, representing a convex object.
		public List<List<BrushSide>> brushes = new();

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
			public string texture = "";

			/// Source vertices (for precision)
			/// Observed TB behaviour is to carry these over.
			public Vector3d vertexA, vertexB, vertexC;

			/// Plane.
			public Plane3d Plane {
				[MethodImpl(MethodImplOptions.AggressiveInlining)]
				get => new Plane3d(vertexA, vertexB, vertexC);
			}

			/// Unnormalized (i.e. immediately usable) texture matrix.
			public Vector3d texSAxis, texTAxis;

			/// 'Rotation' reference value.
			public double rotation;

			/// Basis for texture matrix.
			public Vector2d texOffset;

			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public Vector2d MapUV(Vector3d i) {
				return texOffset + new Vector2d((i * texSAxis).Sum, (i * texTAxis).Sum);
			}
		}
	}
}