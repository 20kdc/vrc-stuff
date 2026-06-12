using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using KDCVRCBSP.ECL;

namespace KDCVRCBSP {
	/**
	 * Entity parameterizer for func_pickup.
	 */
	public class KDCBSPRigidbodyEntityParameterizer : KDCBSPEntity {
		public override void EntityCompile(IKDCBSPImportContext importContext, ECLBSPFile.Entity entity, string uniqueName) {
			base.EntityCompile(importContext, entity, uniqueName);
			bool kinematic = entity.GetBool("kinematic", true);
			bool gravity = entity.GetBool("gravity", false);
			GetComponent<Rigidbody>().isKinematic = kinematic;
			GetComponent<Rigidbody>().useGravity = gravity;
		}
	}
}
