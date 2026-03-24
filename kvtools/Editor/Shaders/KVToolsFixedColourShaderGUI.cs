using System;
using System.IO;
using UnityEngine;
using UnityEditor;
using System.Reflection;

namespace KDCVRCTools {
	public class KVToolsFixedColourShaderGUI : ShaderGUI {
		private static GUIContent RenderText = new GUIContent("Render Colour");
		private static GUIContent EmissionText = new GUIContent("Bake/Emission Colour");

		public override void OnGUI(MaterialEditor materialEditor, MaterialProperty[] props) {
			var renderColour = FindProperty("_Color", props);
			var emissionColour = FindProperty("_EmissionColor", props);

			Material material = (Material) materialEditor.target;

			materialEditor.SetDefaultGUIWidths();

			EditorGUI.BeginChangeCheck();

			EditorGUI.showMixedValue = renderColour.hasMixedValue;
			EditorGUILayout.ColorField(RenderText, renderColour.colorValue, true, false, false);
			EditorGUI.showMixedValue = false;

			if (materialEditor.EmissionEnabledProperty()) {
				EditorGUI.showMixedValue = emissionColour.hasMixedValue;
				EditorGUILayout.ColorField(EmissionText, emissionColour.colorValue, true, false, true);
				EditorGUI.showMixedValue = false;
				materialEditor.LightmapEmissionFlagsProperty(MaterialEditor.kMiniTextureFieldLabelIndentLevel, true);
			}

			EditorGUILayout.Space();

			materialEditor.RenderQueueField();
			materialEditor.EnableInstancingField();
			materialEditor.DoubleSidedGIField();

			if (EditorGUI.EndChangeCheck())
				FixKeywords(material);
		}

		public override void AssignNewShaderToMaterial(Material material, Shader oldShader, Shader newShader) {
			base.AssignNewShaderToMaterial(material, oldShader, newShader);
			FixKeywords(material);
		}

		/**
		 * Fixes up the one keyword that needs to be fixed here.
		 */
		public static void FixKeywords(Material material) {
			MaterialEditor.FixupEmissiveFlag(material);
			if ((material.globalIlluminationFlags & MaterialGlobalIlluminationFlags.EmissiveIsBlack) == 0) {
				material.EnableKeyword("_EMISSION");
			} else {
				material.DisableKeyword("_EMISSION");
			}
		}
	}
}
