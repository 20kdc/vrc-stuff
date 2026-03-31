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
		bool kinematic;
		public override void EntityParameterize(KDCBSPIntermediate bsp, ref KDCBSPIntermediate.Entity entity, string uniqueName) {
			kinematic = entity.GetBool("kinematic", true);
			bool gravity = entity.GetBool("gravity", false);
			GetComponent<Rigidbody>().isKinematic = kinematic;
			GetComponent<Rigidbody>().useGravity = gravity;
		}

		public override KDCBSPBrushEntitySettings EntityGetBrushSettings(bool isWorldspawn, KDCBSPBrushEntitySettings worldspawnCompilation, KDCBSPBrushEntitySettings brushEntityCompilation) {
			brushEntityCompilation.collision = KDCBSPBrushEntitySettings.CollisionMode.SingleConvexRoot;
			brushEntityCompilation.collisionIsTrigger = kinematic;
			return brushEntityCompilation;
		}
	}
}
