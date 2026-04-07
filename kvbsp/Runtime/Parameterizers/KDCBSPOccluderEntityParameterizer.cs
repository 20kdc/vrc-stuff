using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using VRC.SDKBase;

namespace KDCVRCBSP {
	/**
	 * Entity parameterizer for func_occluder.
	 */
	public class KDCBSPOccluderEntityParameterizer : KDCBSPEntityParameterizer {
		public override void EntityParameterize(KDCBSPIntermediate bsp, ref KDCBSPIntermediate.Entity entity, string uniqueName) {
			bool open = entity.GetBool("open", false);
			var portal = GetComponent<OcclusionPortal>();
			portal.open = open;
			bsp.GetEntityBox(entity, out Vector3 centre, out Vector3 size);
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
		public override KDCBSPBrushEntitySettings EntityGetBrushSettings(bool isWorldspawn, KDCBSPBrushEntitySettings worldspawnCompilation, KDCBSPBrushEntitySettings brushEntityCompilation) {
			return new KDCBSPBrushEntitySettings {
				visuals = false,
				collision = KDCBSPBrushEntitySettings.CollisionMode.None
			};
		}
	}
}
