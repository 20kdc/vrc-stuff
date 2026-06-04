using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using KDCVRCBSP.ECL;

namespace KDCVRCBSP {
	/**
	 * Material role.
	 * This is used to keep the workings of the BSP compiler reasonably 'clear'.
	 */
	[CreateAssetMenu(menuName = "KDCVRCTools/KDCBSP Material Role", fileName = "materialRole")]
	public class KDCBSPMaterialRole: ScriptableObject, IBSPMaterial {
		[Tooltip("Surface flags for this surface.")]
		[SerializeField]
		public BSPSurfaceFlags surfaceFlags;

		[Tooltip("Surface flags OR'd to all surfaces on the brush.")]
		[SerializeField]
		public BSPSurfaceFlags transFlags;

		public BSPSurfaceFlags SurfaceFlags {
			get {
				return surfaceFlags;
			}
		}

		public BSPSurfaceFlags TransFlags {
			get {
				return transFlags;
			}
		}
	}
}
