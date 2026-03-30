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
		public override KDCBSPBrushEntitySettings EntityGetBrushSettings(KDCBSPBrushEntitySettings defaultSettings) {
			return new KDCBSPBrushEntitySettings {
				collision = KDCBSPBrushEntitySettings.CollisionMode.SingleConvexRoot,
				collisionIsTrigger = true
			};
		}
	}
}
