using UnityEngine;

namespace KDCVRCBSP {
	/**
	 * This entity parameterizer is intended as a good 'middle ground' descriptor to avoid duplicating logic.
	 */
	public class KDCBSPEntityDescriptor : KDCBSPEntityParameterizer {
		[Tooltip("If enabled, then brush entity compilation will use the given brush entity settings rather than worldspawn's.")]
		[SerializeField]
		public bool useBrushEntitySettings;

		[Tooltip("Brush entity settings (see useBrushEntitySettings).")]
		[SerializeField]
		public KDCBSPBrushEntitySettings brushEntitySettings = new();

		public override KDCBSPBrushEntitySettings EntityGetBrushSettings(bool isWorldspawn, KDCBSPBrushEntitySettings prev) {
			return useBrushEntitySettings ? prev : (KDCBSPBrushEntitySettings) brushEntitySettings.Clone();
		}
	}
}
