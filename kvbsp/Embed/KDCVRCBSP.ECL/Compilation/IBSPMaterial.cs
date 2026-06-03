using System;

namespace KDCVRCBSP.ECL {
	/// This interface provides information to the 'fixed-function' stages of the BSP compiler.
	public interface IBSPMaterial {
		/// Surface flags for this material.
		public BSPSurfaceFlags SurfaceFlags { get; }
	}
}
