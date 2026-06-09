using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using KDCVRCBSP.ECL;

namespace KDCVRCBSP {
	/**
	 * Entity parameterizer for func_occluder.
	 */
	public class KDCBSPOccluderEntityParameterizer : KDCBSPEntityParameterizer {
		public override void EntityParameterize(ECLBSPFile bsp, ECLBSPFile.Entity entity, string uniqueName, float worldScale) {
			bool open = entity.GetBool("open", false);
			var portal = GetComponent<OcclusionPortal>();
			portal.open = open;
			KDCBSPUtilities.GetEntityBox(entity, worldScale, out Vector3 centre, out Vector3 size);
#if UNITY_EDITOR
			// this is so stupid!
			using (var so = new UnityEditor.SerializedObject(portal)) {
				so.Update();
				so.FindProperty("m_Center").vector3Value = centre;
				so.FindProperty("m_Size").vector3Value = size;
				so.ApplyModifiedProperties();
			}
#endif
		}
		public override KDCBSPBrushEntitySettings EntityGetBrushSettings(bool isWorldspawn, KDCBSPBrushEntitySettings prev) {
			return new KDCBSPBrushEntitySettings {
				visuals = false,
				collision = KDCBSPBrushEntitySettings.CollisionMode.None
			};
		}
	}
}
