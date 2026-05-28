using System;
using System.Runtime.CompilerServices;

namespace KDCVRCBSP.ECL {
	/// Geometry utilities / maths extensions
	public static class GeomUtil {
		/// Returns which point a side is on relative to this plane.
		/// 1 is above, -1 is below, 0 is 'on' (within epsilon tolerance).
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static int SignedDistanceToSide(double dist, double epsilon) {
			if (Math.Abs(dist) < epsilon)
				return 0;
			if (dist < 0)
				return -1;
			return 1;			
		}
	}
}