using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Runtime.CompilerServices;

namespace KDCVRCBSP.ECL {
	/// TODO
	public class Epsilons {
		/// Distance epsilon, used for comparing co-planarity in various circumstances (among other things)
		public double distance = 1d / 256d;

		/// Broadphase epsilon. This is supplied to AABB3d.Intersects.
		public double broadphase = 0.5d;

		/// Normal epsilon, used for comparing normals in co-planarity checks.
		public double normal = 1d / 256d;

		/// UV or BrushUV epsilon
		public double uv = 1d / 256d;

		/// Initial winding size.
		public double initialWindingSize = 65536d;

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool PlaneNearPlane(Plane3d a, Plane3d b) {
			return NormalNearNormal(a.normal, b.normal) && (Math.Abs(a.distance - b.distance) < distance);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool NormalNearNormal(Vector3d a, Vector3d b) {
			var comp = a - b;
			return Math.Abs(comp.x) < normal && Math.Abs(comp.y) < normal && Math.Abs(comp.z) < normal;
		}
	}
}
