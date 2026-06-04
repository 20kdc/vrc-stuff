using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using KDCVRCBSP.ECL;

namespace KDCVRCBSP {
	/**
	 * This type should only ever be extended by KDCBSPAbstractMaterialConfig in the editor assembly.
	 * This type allows Runtime assemblies to hold references to editor material configs.
	 */
	public abstract class KDCBSPAbstractMaterialConfigRuntime : ScriptableObject, IBSPMaterial {
		[Tooltip("The internal BSP compiler uses this role to decide how to handle the material.")]
		[SerializeField]
		public KDCBSPMaterialRole bspRole;

		public BSPSurfaceFlags SurfaceFlags {
			get {
				if (bspRole == null)
					return 0;
				return bspRole.SurfaceFlags;
			}
		}

		public BSPSurfaceFlags TransFlags {
			get {
				if (bspRole == null)
					return 0;
				return bspRole.TransFlags;
			}
		}
	}
}
