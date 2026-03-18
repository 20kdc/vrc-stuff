using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEditor;
using UnityEditor.AssetImporters;

namespace KDCVRCBSP {
	[CreateAssetMenu(menuName = "KDCVRCTools/KDCBSP Workspace Config", fileName = "kdcbspWorkspaceConfig")]
	public class KDCBSPWorkspaceConfig : ScriptableObject {

		// [TRANSFORM]
		// This is calibrated to vr_player_stick
		[Tooltip("How many Quake units map to 1 metre?")]
		[SerializeField]
		public float worldScale = 64.0f;

		[Tooltip("List of material assignments.")]
		[SerializeField]
		public List<MaterialAssignment> materials = new();

		[SerializeField]
		public MaterialAssignment fallbackMaterial = new MaterialAssignment {
			name = "",
			width = 1.0f,
			height = 1.0f,
			material = null
		};

		[System.Serializable]
		public class MaterialAssignment {
			[Tooltip("Quake 2 material name. Max 32 ASCII characters.")]
			[SerializeField]
			public string name = "";

			[Tooltip("Size of the TrenchBroom 'proxy' material.")]
			[SerializeField]
			public float width = 1.0f;

			[Tooltip("Size of the TrenchBroom 'proxy' material.")]
			[SerializeField]
			public float height = 1.0f;

			/// Sets the material.
			[Tooltip("Unity material. If not set, this material won't be rendered (but will still exist for Concave collider mode).")]
			[SerializeField]
			public LazyLoadReference<Material> material;

			/// Contains a possible 'template gameobject'.
			/// This template is used for each material renderer, and (importantly) may carry the settings used by it.
			/// If not set, then a default setup is used.
			/// Components are automatically created if missing.
			[Tooltip("Possible 'template gameobject' for rendering. Can contain arbitrary MeshRenderer settings. Material is overridden with the given material, and static flags may be overridden if set in import.")]
			[SerializeField]
			public LazyLoadReference<GameObject> rendererTemplate;
		}
	}
}
