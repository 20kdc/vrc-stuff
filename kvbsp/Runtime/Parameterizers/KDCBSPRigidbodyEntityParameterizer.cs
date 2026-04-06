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
	public class KDCBSPRigidbodyEntityParameterizer : KDCBSPEntityParameterizer {
		public override void EntityParameterize(KDCBSPIntermediate bsp, ref KDCBSPIntermediate.Entity entity, string uniqueName) {
			bool kinematic = entity.GetBool("kinematic", true);
			bool gravity = entity.GetBool("gravity", false);
			GetComponent<Rigidbody>().isKinematic = kinematic;
			GetComponent<Rigidbody>().useGravity = gravity;
		}
	}
}
