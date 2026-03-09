using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.AssetImporters;

namespace KDCVRCTools {
	[CreateAssetMenu(menuName = "KDCVRCTools/KDCBSP Workspace Config", fileName = "kdcbspWorkspaceConfig")]
	public class KDCBSPWorkspaceConfig : ScriptableObject {

		// [TRANSFORM]
		// This is calibrated to vr_player_stick
		[SerializeField]
		public float worldScale = 64.0f;

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
			[SerializeField]
			public string name = "";
			[SerializeField]
			public float width = 1.0f;
			[SerializeField]
			public float height = 1.0f;
			[SerializeField]
			public Material material = null;
		}
	}
}
