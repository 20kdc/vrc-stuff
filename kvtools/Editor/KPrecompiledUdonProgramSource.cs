using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using VRC.Udon;
using VRC.Udon;
using VRC.Udon.Common.Interfaces;
using VRC.Udon.ProgramSources;
using VRC.Udon.Editor.ProgramSources;

namespace KDCVRCTools {
	/**
	 * So basically, this bridges the mess that is UdonProgramAsset to our shiny up-and-coming SerializedUdonProgramAsset-based workflow.
	 * UdonProgramAsset, for some reason, handles a number of concerns that really should be in UdonBehaviour.
	 * The 'upside' to this 'flexibility' is that it allows asset creators to completely override the GUI for their assets using a proxy behaviour.
	 * The downside is that because we can't rely on IUdonProgram being the be-all-end-all of an Udon program, we need to do some pretty horrible stuff here.
	 */
	[CreateAssetMenu(menuName = "VRChat/Udon/KDCVRCTools: Precompiled Udon Program Asset", fileName = "New Precompiled Udon Program Asset")]
	public class PrecompiledUdonProgramSource : UdonProgramAsset {

		public AbstractSerializedUdonProgramAsset SourceSerializedProgramAsset {
			get {
				return serializedUdonProgramAsset;
			}
			set {
				serializedUdonProgramAsset = value;
				program = null;
				if (serializedUdonProgramAsset != null)
					program = serializedUdonProgramAsset.RetrieveProgram();
			}
		}

		public override AbstractSerializedUdonProgramAsset SerializedProgramAsset {
			get {
				return serializedUdonProgramAsset;
			}
		}

		protected override void DrawProgramSourceGUI(UdonBehaviour udonBehaviour, ref bool dirty) {
			DrawPublicVariables(udonBehaviour, ref dirty);
			DrawProgramDisassembly();
		}

		protected override void RefreshProgramImpl() {
			if (serializedUdonProgramAsset != null)
				program = serializedUdonProgramAsset.RetrieveProgram();
			// We will inevitably suffer VRCSDK attempting to mutate the serializedUdonProgramAsset.
			// The consequences of this aren't 'too' severe; we'll let it happen. :(
		}
	}
}
