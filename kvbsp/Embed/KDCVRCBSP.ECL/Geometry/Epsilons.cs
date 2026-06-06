using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Runtime.CompilerServices;

namespace KDCVRCBSP.ECL {
	/// TODO
	public class Epsilons {
		/// Distance epsilon, used for comparing co-planarity in various circumstances (among other things)
		public double distanceEpsilon = 1d / 256d;

		/// Broadphase epsilon. This is supplied to AABB3d.Intersects.
		public double broadphaseEpsilon = 0.5d;

		/// Normal epsilon, used for comparing normals in co-planarity checks.
		public double normalEpsilon = 1d / 256d;

		/// Initial winding size.
		public double initialWindingSize = 65536d;
	}
}
