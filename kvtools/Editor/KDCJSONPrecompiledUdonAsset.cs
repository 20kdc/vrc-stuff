using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using VRC.Udon;
using VRC.Udon.Common.Interfaces;
using VRC.Udon.ProgramSources;
using VRC.Udon.Editor.ProgramSources;
using VRC.SDK3.UdonNetworkCalling;
using VRC.Udon.Editor;

namespace KDCVRCTools {
	/**
	 * So after the whole signatures thing made it pretty clear a direct translation wasn't going to work too well, I decided on a new approach.
	 * The plan is pretty simple: We keep a SerializedUdonProgramAsset in JSON form.
	 * That's the whole plan.
	 */
	public class KDCJSONPrecompiledUdonAsset : UdonProgramAsset {

		[SerializeField]
		public string InternalJSON = "{}";

		protected SerializedUdonProgramAsset lastSerializedUdonProgramAsset;

		protected override void DrawProgramSourceGUI(UdonBehaviour udonBehaviour, ref bool dirty) {
			if(GUILayout.Button("Refresh Program"))
				UdonEditorManager.Instance.QueueAndRefreshProgram(this);
			DrawInteractionArea(udonBehaviour);
			DrawPublicVariables(udonBehaviour, ref dirty);
			DrawProgramDisassembly();
		}

		protected override void RefreshProgramImpl() {
			if (lastSerializedUdonProgramAsset != null) {
				// this is too risky, so we'll live with the extremely slow hopefully-GCable memory leak.
				// Object.DestroyImmediate(lastSerializedUdonProgramAsset);
				lastSerializedUdonProgramAsset = null;
			}
			program = null;
			lastSerializedUdonProgramAsset = ScriptableObject.CreateInstance<SerializedUdonProgramAsset>();
			JsonUtility.FromJsonOverwrite(InternalJSON, lastSerializedUdonProgramAsset);
			program = lastSerializedUdonProgramAsset.RetrieveProgram();
		}

		protected override NetworkCallingEntrypointMetadata[] GetLastNetworkCallingMetadata() {
			if (lastSerializedUdonProgramAsset == null)
				return null;
			return lastSerializedUdonProgramAsset.GetNetworkCallingMetadata();
		}

	}
}
