using System;
using System.IO;
using UnityEngine;
using UnityEditor;

namespace KDCVRCBSP {
	[CustomEditor(typeof(KDCBSPWizard))]
	public class KDCBSPWizardEditor : Editor {

		static int wizardStep = 0;
		static string trenchBroomConfig = "";
		static string qrootPath = "";
		static string qbspPath = "";

		private void Infer() {
			if (trenchBroomConfig == "") {
				// try guess TrenchBroom config
				string windowsChk = Environment.GetEnvironmentVariable("AppData");
				if (windowsChk != null) {
					// assume Windows
					trenchBroomConfig = Path.Join(windowsChk, "TrenchBroom");
				} else {
					// could be Mac or some non-Mac Unix
					// to disambiguate, look for what Mac will always have
					string home = Environment.GetEnvironmentVariable("HOME");
					if (home != null) {
						if (Directory.Exists(home + "/Library/Application Support")) {
							// assume Mac
							trenchBroomConfig = Path.Join(home, "Library/Application Support/TrenchBroom");
						} else {
							// assume other
							trenchBroomConfig = Path.Join(home, ".TrenchBroom");
						}
					}
				}
			}
			if (qrootPath == "") {
				qrootPath = FileUtil.GetPhysicalPath("Assets/KDCBSPGameRoot");
			}
			if (qbspPath == "") {
				// dev privileges
				qbspPath = "qbsp";
				string home = Environment.GetEnvironmentVariable("HOME");
				if (home != null) {
					if (File.Exists(home + "/.local/bin/qbsp")) {
						qbspPath = home + "/.local/bin/qbsp";
					}
				}
			}
		}

		void OnEnable() {
			Infer();
		}

		private void WWLabel(string text) {
			GUILayout.Label(text, EditorStyles.wordWrappedLabel);
		}

		public override void OnInspectorGUI() {
			// discovery
			string trenchBroomConfigGames = Path.Join(trenchBroomConfig, "games");
			string trenchBroomConfigDetail = Path.Join(trenchBroomConfigGames, "KVToolsTB");
			string trenchBroomConfigCompilationProfiles = Path.Join(trenchBroomConfigDetail, "CompilationProfiles.cfg");
			bool compilationProfilesAlreadyExist = File.Exists(trenchBroomConfigCompilationProfiles);

			if (wizardStep == 0) {
				WWLabel("Hey! Welcome to the t20kdc.vrc-bsp setup wizard!");
				WWLabel("This is designed to try and help get you up and running as quickly as possible.");
				if (GUILayout.Button("Firstly, ensure you've installed TrenchBroom.")) {
					Application.OpenURL("https://github.com/TrenchBroom/TrenchBroom");
				}
				WWLabel("TrenchBroom's user directory is assumed to be:");
				trenchBroomConfig = EditorGUILayout.TextField("TrenchBroom Config", trenchBroomConfig);
				WWLabel("If it's different, change it now.");
				WWLabel("If using TrenchBroom in portable mode, the executable directory is also the user directory.");
				bool problem = false;
				if (!Directory.Exists(trenchBroomConfig)) {
					EditorGUILayout.HelpBox("The directory doesn't exist. Run TrenchBroom or adjust accordingly.", MessageType.Error);
					problem = true;
				} else if (!File.Exists(Path.Join(trenchBroomConfig, "Preferences.json"))) {
					EditorGUILayout.HelpBox("Preferences.json doesn't exist. Wrong directory, or you haven't run TrenchBroom yet?", MessageType.Error);
					problem = true;
				}
				if (problem) {
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
					qbspPath = EditorGUILayout.TextField("ericw-tools qbsp Path", qbspPath);
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
					try {
						Directory.CreateDirectory(trenchBroomConfigGames);
					} catch (Exception ex) {
						Debug.LogException(ex);
					}
					try {
						Directory.CreateDirectory(trenchBroomConfigDetail);
						string[] trivial = {"CompilationProfiles.cfg", "GameConfig.cfg", "kvtoolstb.fgd"};
						foreach (string v in trivial) {
							string fileFrom = FileUtil.GetPhysicalPath("Packages/t20kdc.vrc-bsp/TrenchBroom~/KVToolsTB/" + v);
							string fileTo = Path.Join(trenchBroomConfigDetail, v);
							if (v == "CompilationProfiles.cfg" && compilationProfilesAlreadyExist)
								continue;
							File.WriteAllText(fileTo, File.ReadAllText(fileFrom).Replace("TOOL_QBSP", qbspPath), new System.Text.UTF8Encoding(false));
						}
					} catch (Exception ex) {
						Debug.LogException(ex);
					}
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
						FileUtil.CopyFileOrDirectory("Packages/t20kdc.vrc-bsp/TrenchBroom~/KDCBSPGameRoot", qrootPath);
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
