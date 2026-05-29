namespace KDCVRCBSP.ECL {
    using System.Collections.Generic;
    using System.Runtime.CompilerServices;
    using VectorD = Vector3d;
	using Self = AABB3d;

	/// Bounding box.
	public struct AABB3d {
		public VectorD min;
		public VectorD max;

		/// Creates an AABB from a set of points.
		public AABB3d(IEnumerable<VectorD> source) {
			bool first = true;
			min = VectorD.Zero;
			max = VectorD.Zero;
			foreach (var v in source) {
				if (first) {
					min = v;
					max = v;
				} else {
					min = min.Min(v);
					max = max.Max(v);
				}
			}
		}

		/// Merges a point into the AABB.
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public Self Join(VectorD point) => new Self {
			min = point.Min(min),
			max = point.Max(max)
		};

		/// Merges another AABB into the AABB.
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public Self Join(Self point) => new Self {
			min = point.min.Min(min),
			max = point.max.Max(max)
		};

		/// If this AABB intersects with another.
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool Intersects(AABB3d other) {
			if (min.x >= other.max.x || max.x <= other.min.x)
				return false;
			if (min.y >= other.max.y || max.y <= other.min.y)
				return false;
			if (min.z >= other.max.z || max.z <= other.min.z)
				return false;
			return true;
		}
	}
}