using System;
using System.Collections.Generic;

namespace KDCVRCBSP.ECL {
	/// Represents a parsed map entity.
	/// A parsed map is simply a list of entities, so it's a big deal that we get these right.
	public sealed class EntityParsed {
		/// List of key/value pairs.
		public List<(string, string)> pairs = new();

		/// List of brushes as lists of brush sides.
		/// A brush is the fundamental geometric primitive, representing a convex object.
		public List<List<BrushSide>> brushes = new();

		/// A brush side represents a side of a brush.
		/// A brush is a list of brush sides.
		public sealed class BrushSide {
			public string texture = "";
			public Plane3d plane;
			public double sX, sY, sZ, sO, tX, tY, tZ, tO;
			public Vector2d MapUV(Vector3d i) {
				return (sO + (i.x * sX) + (i.y * sY) + (i.z * sZ), tO + (i.x * tX) + (i.y * tY) + (i.z * tZ));
			}
		}
	}
}