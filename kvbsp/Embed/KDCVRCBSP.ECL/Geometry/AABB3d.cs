namespace KDCVRCBSP.ECL {
	using System.Collections.Generic;
	using System.Runtime.CompilerServices;
	using VectorD = Vector3d;
	using Self = AABB3d;

	/// Bounding box.
	public struct AABB3d {
		public VectorD min;
		public VectorD max;

		public AABB3d(VectorD min, VectorD max) {
			this.min = min;
			this.max = max;
		}

		/// Creates an AABB from a set of points.
		public AABB3d(IEnumerable<VectorD> source) {
			bool first = true;
			min = VectorD.Zero;
			max = VectorD.Zero;
			foreach (var v in source) {
				if (first) {
					min = v;
					max = v;
					first = false;
				} else {
					min = min.Min(v);
					max = max.Max(v);
				}
			}
		}

		public double Volume {
			get {
				var size = max - min;
				return size.x * size.y * size.z;
			}
		}

		/// Create axial planes.
		public Plane3d GenAxialPlane(int id, double nudge) {
			double src;
			Vector3d normal;
			bool flip = false;
			if (id == 0) {
				normal = new Vector3d(1, 0, 0);
				src = max.x + nudge;
			} else if (id == 1) {
				normal = new Vector3d(-1, 0, 0);
				src = min.x - nudge;
				flip = true;
			} else if (id == 2) {
				normal = new Vector3d(0, 1, 0);
				src = max.y + nudge;
			} else if (id == 3) {
				normal = new Vector3d(0, -1, 0);
				src = min.y - nudge;
				flip = true;
			} else if (id == 4) {
				normal = new Vector3d(0, 0, 1);
				src = max.z + nudge;
			} else {
				normal = new Vector3d(0, 0, -1);
				src = min.z - nudge;
				flip = true;
			}
			return new Plane3d(normal, flip ? -src : src);
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
		/// If 'margin' is above 0, it biases in favour of intersect.
		/// If below 0, it biases against intersect.
		/// Beware that use of the margin may make 'zero' AABBs intersect each other.
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool Intersects(AABB3d other, double margin) {
			if (min.x >= (other.max.x + margin) || max.x <= (other.min.x - margin))
				return false;
			if (min.y >= (other.max.y + margin) || max.y <= (other.min.y - margin))
				return false;
			if (min.z >= (other.max.z + margin) || max.z <= (other.min.z - margin))
				return false;
			return true;
		}

		/// If this AABB contains a point.
		/// If 'margin' is above 0, it biases in favour of intersect.
		/// If below 0, it biases against intersect.
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool Contains(Vector3d other, double margin) {
			if (min.x >= (other.x + margin) || max.x <= (other.x - margin))
				return false;
			if (min.y >= (other.y + margin) || max.y <= (other.y - margin))
				return false;
			if (min.z >= (other.z + margin) || max.z <= (other.z - margin))
				return false;
			return true;
		}
	}
}
