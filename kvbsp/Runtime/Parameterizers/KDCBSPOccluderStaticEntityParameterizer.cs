using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using VRC.SDKBase;

using FlagMod = KDCVRCBSP.KDCBSPBrushEntitySettings.FlagMod;

namespace KDCVRCBSP {
	/**
	 * Entity parameterizer for func_occluder.
	 */
	public class KDCBSPOccluderStaticEntityParameterizer : KDCBSPEntityParameterizer {
		public override KDCBSPBrushEntitySettings EntityGetBrushSettings(bool isWorldspawn, KDCBSPBrushEntitySettings prev) {
			return new KDCBSPBrushEntitySettings {
				visuals = true,
				contributeGI = KDCBSPBrushEntitySettings.FlagMod.Off,
				lightmaps = KDCBSPBrushEntitySettings.FlagMod.Off,
				occluderStatic = KDCBSPBrushEntitySettings.FlagMod.On,
				occludeeStatic = KDCBSPBrushEntitySettings.FlagMod.On,
				reflectionProbeStatic = KDCBSPBrushEntitySettings.FlagMod.Off,
				collision = KDCBSPBrushEntitySettings.CollisionMode.None
			};
		}
	}
}
