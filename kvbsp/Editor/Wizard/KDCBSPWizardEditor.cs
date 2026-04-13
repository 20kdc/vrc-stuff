using System;
using System.IO;
using UnityEngine;
using UnityEditor;

namespace KDCVRCBSP {
	[CustomEditor(typeof(KDCBSPWizard))]
	public class KDCBSPWizardEditor : Editor {

		static int wizardStep = 0;

		void OnEnable() {
		}

		private void WWLabel(string text) {
			GUILayout.Label(text, EditorStyles.wordWrappedLabel);
		}

		public override void OnInspectorGUI() {
			string qrootPath = FileUtil.GetPhysicalPath("Assets/KDCBSPGameRoot");
			// ...
			string trenchBroomConfig = KDCBSPSetupCore.PathTrenchBroomConfig;
			string qbspPath = KDCBSPSetupCore.PathQBSP;
			// discovery
			bool compilationProfilesAlreadyExist = File.Exists(KDCBSPSetupCore.PathTrenchBroomCompilationProfiles);

			if (wizardStep == 0) {
				WWLabel("Hey! Welcome to the t20kdc.vrc-bsp setup wizard!");
				WWLabel("This is designed to try and help get you up and running as quickly as possible.");
				if (GUILayout.Button("Firstly, ensure you've installed TrenchBroom.")) {
					Application.OpenURL("https://github.com/TrenchBroom/TrenchBroom");
				}
				WWLabel("TrenchBroom's user directory is assumed to be:");

				string trenchBroomConfigNew = EditorGUILayout.TextField("TrenchBroom Config", trenchBroomConfig);
				if (trenchBroomConfigNew != trenchBroomConfig)
					KDCBSPSetupCore.PathTrenchBroomConfig = trenchBroomConfigNew;
				EditorGUILayout.BeginHorizontal();
				WWLabel("If it's different, change it now.");
				if (GUILayout.Button("Force redetect (if newly installed/run)")) {
					KDCBSPSetupCore.PathTrenchBroomConfig = null;
				}
				EditorGUILayout.EndHorizontal();

				WWLabel("If using TrenchBroom in portable mode, the executable directory is also the user directory.");
				string problem = KDCBSPSetupCore.IssueTrenchBroomConfig;
				if (problem != null) {
					EditorGUILayout.HelpBox(problem, MessageType.Error);
					if (GUILayout.Button("Consider referring to the TrenchBroom manual.")) {
						Application.OpenURL("https://trenchbroom.github.io/manual/latest/#game_configuration_files");
					}
				} else {
					if (GUILayout.Button("Continue.")) {
						wizardStep = 1;
					}
				}
			} else if (wizardStep == 1) {
				WWLabel("If you haven't already, grab ericw-tools. It's important to use something at least as new as a 2.0.0 alpha build, for the Quake 2 support.");
				if (GUILayout.Button("Link")) {
					Application.OpenURL("https://github.com/ericwa/ericw-tools/releases");
				}
				if (!compilationProfilesAlreadyExist) {
					string qbspPathNew = EditorGUILayout.TextField("ericw-tools qbsp Path", qbspPath);
					if (qbspPathNew != qbspPath)
						KDCBSPSetupCore.PathQBSP = qbspPathNew;
					if (!File.Exists(qbspPath)) {
						EditorGUILayout.HelpBox("The QBSP executable doesn't exist.", MessageType.Error);
					} else {
						if (GUILayout.Button("Continue.")) {
							wizardStep = 2;
						}
					}
				} else {
					EditorGUILayout.HelpBox("The CompilationProfiles.cfg file already exists, and won't be overwritten, so you don't need to find qbsp.", MessageType.Info);
					if (GUILayout.Button("Continue.")) {
						wizardStep = 2;
					}
				}
			} else if (wizardStep == 2) {
				WWLabel("The game definition will now be installed into your TrenchBroom configuration.");
				if (GUILayout.Button("Continue (copy/update game definition)")) {
					KDCBSPSetupCore.RunTrenchBroomSetup();
					wizardStep = 3;
				}
				if (GUILayout.Button("Skip (if setup already)")) {
					wizardStep = 3;
				}
			} else if (wizardStep == 3) {
				WWLabel("A quick heads-up. When working with KDCBSP, keep in mind that textures in TrenchBroom are not materials in Unity.");
				WWLabel("You create textures in TrenchBroom by dropping in image files into the correct directory.");
				WWLabel("You attach these to materials in Unity by assigning them in a KDCBSP workspace configuration.");
				WWLabel("We'll now create the 'game root'. This is where the TrenchBroom-side textures live.");
				WWLabel("This will be created at: " + qrootPath);
				if (GUILayout.Button("Continue (setup game root)")) {
					try {
						FileUtil.CopyFileOrDirectory(KDCBSPUtilities.KVBSP_BASE + "TrenchBroom~/KDCBSPGameRoot", qrootPath);
					} catch (Exception ex) {
						Debug.LogException(ex);
					}
					AssetDatabase.Refresh();
					wizardStep = 4;
				}
				if (GUILayout.Button("Skip (if setup already)")) {
					wizardStep = 4;
				}
			} else if (wizardStep == 4) {
				WWLabel("You should have an example map file at: " + qrootPath + "/example.map");
				WWLabel("Try compiling it in TrenchBroom:");
				WWLabel("1. Open TrenchBroom");
				WWLabel("2. Select the KVToolsTB engine");
				WWLabel("This may ask you for the game path: " + qrootPath);
				WWLabel("3. Open the map file");
				WWLabel("4. Press Run/Compile Map, and hit Compile");
				WWLabel("5. Return to Unity, and you should have the room as a prefab!");
				WWLabel("Key notes:");
				WWLabel("READ THE README!!!");
				WWLabel("It gives a much better explanation of how everything works than this text can.");
			} else {
				WWLabel("Unknown wizard step! How'd you manage that? Press (Reset Wizard).");
			}
			if (GUILayout.Button("(Reset Wizard)")) {
				wizardStep = 0;
			}
		}
	}
}
