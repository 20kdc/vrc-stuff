using System;
using System.IO;
using UnityEngine;
using UnityEditor;

namespace KDCVRCBSP {
	[CustomEditor(typeof(KDCBSPWizard))]
	public class KDCBSPWizardEditor : Editor {
		private Action[] wizardSteps;
		bool foldoutLinux = false;

		public KDCBSPWizardEditor() {
			wizardSteps = new Action[] {
				WIZ_Open,
				// tb
				WIZ_TB_Open,
				WIZ_TB_FindQBSP,
				WIZ_TB_ConfirmGameDefInstall,
				// radiant
				WIZ_Radiant_Open,
				WIZ_Radiant_ConfirmGamepackInstall,
				// common *but...*
				WIZ_TB_GameRoot,
				WIZ_Radiant_GameRoot,
				WIZ_TB_AboutWorkspaceConfig,
				WIZ_Radiant_AboutWorkspaceConfig,
				// final notes
				WIZ_TB_FinalNotes,
				WIZ_Radiant_FinalNotes
			};
		}

		void OnEnable() {
		}

		private static void WWLabel(string text) {
			GUILayout.Label(text, EditorStyles.wordWrappedLabel);
		}

		public override void OnInspectorGUI() {
			int wizardStepId = 0;
			if (EditorPrefs.HasKey("KDCBSP_WizardStepID"))
				int.TryParse(EditorPrefs.GetString("KDCBSP_WizardStepID"), out wizardStepId);
			if (wizardStepId < 0 || wizardStepId >= wizardSteps.Length)
				wizardStepId = 0;
			wizardSteps[wizardStepId]();
			if (GUILayout.Button("(Reset Wizard)")) {
				SetWizardStep(WIZ_Open);
			}
		}

		private void SetWizardStep(Action step) {
			for (int i = 0; i < wizardSteps.Length; i++)
				if (wizardSteps[i] == step) {
					EditorPrefs.SetString("KDCBSP_WizardStepID", "" + i);
					return;
				}
			Debug.LogWarning("SetWizardStep can't set a step which doesn't exist");
		}

		private void WIZ_Open() {
			WWLabel("Hey! Welcome to the t20kdc.vrc-bsp setup wizard!");
			WWLabel("This is designed to try and help get you up and running as quickly as possible.");
			WWLabel("Firstly, there are a few map editors, but things can be summarized as NetRadiant-custom and TrenchBroom.");
			WWLabel("TrenchBroom is easier to work with, but doesn't support Quake 3's bezier patches (curved surfaces).");
			WWLabel("NetRadiant-custom is more complex to work with, but supports the Quake 3 bezier patches.");

			if (GUILayout.Button("https://github.com/TrenchBroom/TrenchBroom")) {
				Application.OpenURL("https://github.com/TrenchBroom/TrenchBroom");
			}
			if (GUILayout.Button("https://github.com/Garux/netradiant-custom")) {
				Application.OpenURL("https://github.com/Garux/netradiant-custom");
			}

			if (GUILayout.Button("Alright, I've installed TrenchBroom.")) {
				SetWizardStep(WIZ_TB_Open);
			}
			if (GUILayout.Button("Alright, I've installed NetRadiant-custom.")) {
				SetWizardStep(WIZ_Radiant_Open);
			}
		}

		private void WIZ_TB_Open() {
			WWLabel("The setup now needs to configure TrenchBroom.");
			WWLabel("TrenchBroom's user directory is assumed to be:");

			string trenchBroomConfig = KDCBSPSetupCore.PathTrenchBroomConfig;
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
					SetWizardStep(WIZ_TB_FindQBSP);
				}
			}
		}

		private void WIZ_TB_FindQBSP() {
			string qbspPath = KDCBSPSetupCore.PathQBSP;
			bool compilationProfilesAlreadyExist = File.Exists(KDCBSPSetupCore.PathTrenchBroomCompilationProfiles);
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
						SetWizardStep(WIZ_TB_ConfirmGameDefInstall);
					}
				}
			} else {
				EditorGUILayout.HelpBox("The CompilationProfiles.cfg file already exists, and won't be overwritten, so you don't need to find qbsp.", MessageType.Info);
				if (GUILayout.Button("Continue.")) {
					SetWizardStep(WIZ_TB_ConfirmGameDefInstall);
				}
			}
		}

		private void WIZ_TB_ConfirmGameDefInstall() {
			WWLabel("The game definition will now be installed into your TrenchBroom configuration.");
			if (GUILayout.Button("Continue (copy/update game definition)")) {
				KDCBSPSetupCore.RunTrenchBroomSetup();
				SetWizardStep(WIZ_TB_GameRoot);
			}
			if (GUILayout.Button("Skip (if setup already)")) {
				SetWizardStep(WIZ_TB_GameRoot);
			}
		}

		private void WIZ_Radiant_Open() {
			WWLabel("Radiant-series editors store 'gamepacks' in the executable directory.");
			WWLabel("To proceed, the setup must install its own gamepack.");
			if (foldoutLinux = EditorGUILayout.Foldout(foldoutLinux, "In Case Of Linux AppImage")) {
				WWLabel("On Linux, NetRadiant-custom is typically shipped as an AppImage.");
				WWLabel("If this is the case, create a directory, move the AppImage into it, and run:");
				EditorGUILayout.TextField("Command", "./NetRadiant-Custom-x86_64.AppImage --appimage-extract");
				WWLabel("You can then run the application using the 'squashfs-root/AppRun' program.");
				WWLabel("The 'Radiant directory' in this event is 'squashfs-root/usr/bin'.");
			}

			WWLabel("Please indicate the Radiant directory.");
			string radiant = KDCBSPSetupCore.PathRadiant;
			string radiantNew = EditorGUILayout.TextField("Radiant Directory", radiant);
			if (radiantNew != radiant)
				KDCBSPSetupCore.PathRadiant = radiantNew;

			string problem = KDCBSPSetupCore.IssueRadiant;
			if (problem != null) {
				EditorGUILayout.HelpBox(problem, MessageType.Error);
			} else {
				if (GUILayout.Button("Continue.")) {
					SetWizardStep(WIZ_Radiant_ConfirmGamepackInstall);
				}
			}
		}

		private void WIZ_Radiant_ConfirmGamepackInstall() {
			WWLabel("The gamepack will now be installed into Radiant.");
			if (GUILayout.Button("Continue (copy/update gamepack)")) {
				KDCBSPSetupCore.RunRadiantSetup();
				SetWizardStep(WIZ_Radiant_GameRoot);
			}
			if (GUILayout.Button("Skip (if setup already)")) {
				SetWizardStep(WIZ_Radiant_GameRoot);
			}
		}

		// -- common end thunks --

		private void WIZ_TB_GameRoot() {
			WIZ_GameRoot(WIZ_TB_AboutWorkspaceConfig);
		}

		private void WIZ_Radiant_GameRoot() {
			WIZ_GameRoot(WIZ_Radiant_AboutWorkspaceConfig);
		}

		private void WIZ_TB_AboutWorkspaceConfig() {
			WIZ_AboutWorkspaceConfig(WIZ_TB_FinalNotes);
		}

		private void WIZ_Radiant_AboutWorkspaceConfig() {
			WIZ_AboutWorkspaceConfig(WIZ_Radiant_FinalNotes);
		}

		// -- common end --

		private void WIZ_GameRoot(Action next) {
			string qrootPath = FileUtil.GetPhysicalPath("Assets/KDCBSPGameRoot");
			WWLabel("We'll now create the 'game root'. You'll need to point TrenchBroom at this!");
			WWLabel("This will be created at: " + qrootPath);
			if (GUILayout.Button("Continue (setup game root)")) {
				try {
					FileUtil.CopyFileOrDirectory(KDCBSPUtilities.KVBSP_BASE + "Installable~/KDCBSPGameRoot", qrootPath);
				} catch (Exception ex) {
					Debug.LogException(ex);
				}
				AssetDatabase.Refresh();
				SetWizardStep(next);
			}
			if (GUILayout.Button("Skip (if setup already)")) {
				SetWizardStep(next);
			}
		}

		private void WIZ_AboutWorkspaceConfig(Action next) {
			WWLabel("In KDCBSPGameRoot, you need to open DefaultWorkspaceConfig.asset and press Update Quake VFS.");
			WWLabel("You need to do this whenever changing materials so that the map editor and compiler can recognize them.");
			WWLabel("(It was either this or forcing Windows users to use symbolic links.)");
			WWLabel("While this wizard could do this for you, it's an important learning experience to do this yourself now.");
			if (GUILayout.Button("Okay, I did it")) {
				SetWizardStep(next);
			}
		}

		// -- final notes --

		private void WIZ_TB_FinalNotes() {
			string qrootPath = FileUtil.GetPhysicalPath("Assets/KDCBSPGameRoot");
			WWLabel("You should have an example map file at: " + qrootPath + "/example.map");
			WWLabel("Try compiling it in TrenchBroom:");
			WWLabel("1. Open TrenchBroom");
			WWLabel("2. Select the KVToolsTB engine");
			WWLabel("This may ask you for the game path: " + qrootPath);
			WWLabel("(If the game path needs to be changed later, this can be done in View/Preferences.)");
			WWLabel("3. Open the map file");
			WWLabel("4. Press Run/Compile Map, and hit Compile");
			WWLabel("5. Return to Unity, and you should have the room as a prefab!");
			WWLabel("Key notes:");
			WWLabel("READ THE README!!!");
			WWLabel("It gives a much better explanation of how everything works than this text can.");
		}

		private void WIZ_Radiant_FinalNotes() {
			string qrootPath = FileUtil.GetPhysicalPath("Assets/KDCBSPGameRoot");
			WWLabel("You should have an example map file at: " + qrootPath + "/example.map");
			WWLabel("Try compiling it in Radiant:");
			WWLabel("1. Open Radiant");
			WWLabel("2. Select the KVTools game");
			WWLabel("This may ask you for the game path: " + qrootPath);
			WWLabel("(If the game path needs to be changed later, this can be done in Edit/Preferences/Game/Paths.)");
			WWLabel("3. Open the map file");
			WWLabel("4. Press Build/Compile to BSP");
			WWLabel("5. Return to Unity, and you should have the room as a prefab!");
			WWLabel("Key notes:");
			WWLabel("READ THE README!!!");
			WWLabel("It gives a much better explanation of how everything works than this text can.");
		}
	}
}
