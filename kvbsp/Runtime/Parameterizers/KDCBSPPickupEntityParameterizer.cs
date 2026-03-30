using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using VRC.SDKBase;

namespace KDCVRCBSP {
	/**
	 * Entity parameterizer for func_pickup.
	 */
	public class KDCBSPPickupEntityParameterizer : KDCBSPEntityParameterizer {
		public override KDCBSPBrushEntitySettings EntityGetBrushSettings(bool isWorldspawn, KDCBSPBrushEntitySettings worldspawnCompilation, KDCBSPBrushEntitySettings brushEntityCompilation) {
			brushEntityCompilation.collision = KDCBSPBrushEntitySettings.CollisionMode.SingleConvexRoot;
			brushEntityCompilation.collisionIsTrigger = true;
			return brushEntityCompilation;
		}
	}
}
