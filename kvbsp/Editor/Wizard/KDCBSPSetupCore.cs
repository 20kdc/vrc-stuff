using System;
using System.IO;
using UnityEngine;
using UnityEditor;

namespace KDCVRCBSP {
	/// Core functions relating to the initial setup process.
	public static class KDCBSPSetupCore {
		public static string PathTrenchBroomConfig {
			get {
				if (EditorPrefs.HasKey("KDCBSP_TrenchBroomConfig")) {
					return EditorPrefs.GetString("KDCBSP_TrenchBroomConfig");
				} else {
					// try guess TrenchBroom config
					string windowsChk = Environment.GetEnvironmentVariable("AppData");
					if (windowsChk != null) {
						// assume Windows
						return Path.Join(windowsChk, "TrenchBroom");
					} else {
						// could be Mac or some non-Mac Unix
						// to disambiguate, look for what Mac will always have
						string home = Environment.GetEnvironmentVariable("HOME");
						if (home != null) {
							if (Directory.Exists(home + "/Library/Application Support")) {
								// assume Mac
								return Path.Join(home, "Library/Application Support/TrenchBroom");
							} else {
								// assume other
								return Path.Join(home, ".TrenchBroom");
							}
						}
					}
					// unknown
					return "";
				}
			}
			set {
				if (value == null) {
					EditorPrefs.DeleteKey("KDCBSP_TrenchBroomConfig");
				} else {
					EditorPrefs.SetString("KDCBSP_TrenchBroomConfig", value);
				}
			}
		}

		public static string PathQBSP {
			get {
				if (EditorPrefs.HasKey("KDCBSP_QBSP")) {
					return EditorPrefs.GetString("KDCBSP_QBSP");
				} else {
					// makes things simpler for development
					string home = Environment.GetEnvironmentVariable("HOME");
					if (home != null)
						if (File.Exists(home + "/.local/bin/qbsp"))
							return home + "/.local/bin/qbsp";
					return "qbsp";
				}
			}
			set {
				if (value == null) {
					EditorPrefs.DeleteKey("KDCBSP_QBSP");
				} else {
					EditorPrefs.SetString("KDCBSP_QBSP", value);
				}
			}
		}

		public static string IssueTrenchBroomConfig {
			get {
				string trenchBroomConfig = PathTrenchBroomConfig;
				if (!Directory.Exists(trenchBroomConfig)) {
					return "The directory doesn't exist. Run TrenchBroom or adjust accordingly.";
				} else if (!File.Exists(Path.Join(trenchBroomConfig, "Preferences.json"))) {
					return "Preferences.json doesn't exist. Wrong directory, or you haven't run TrenchBroom yet?";
				}
				return null;
			}
		}

		public static string PathTrenchBroomCompilationProfiles => Path.Join(PathTrenchBroomConfig, "games/KVToolsTB/CompilationProfiles.cfg");

		public static void RunTrenchBroomSetup() {
			string trenchBroomConfig = PathTrenchBroomConfig;
			try {
				Directory.CreateDirectory(Path.Join(trenchBroomConfig, "games"));
			} catch (Exception ex) {
				Debug.LogException(ex);
			}

			string gamePath = Path.Join(trenchBroomConfig, "games", "KVToolsTB");
			try {
				Directory.CreateDirectory(Path.Join(gamePath, "assets"));
			} catch (Exception ex) {
				Debug.LogException(ex);
			}

			try {
				Directory.CreateDirectory(gamePath);
				string[] trivial = {"CompilationProfiles.cfg", "GameConfig.cfg", "kvtoolstb.fgd"};
				foreach (string v in trivial) {
					string fileFrom = FileUtil.GetPhysicalPath("Packages/t20kdc.vrc-bsp/TrenchBroom~/KVToolsTB/" + v);
					string fileTo = Path.Join(gamePath, v);
					if (v == "CompilationProfiles.cfg" && File.Exists(fileTo))
						continue;
					File.WriteAllText(fileTo, File.ReadAllText(fileFrom).Replace("TOOL_QBSP", PathQBSP), new System.Text.UTF8Encoding(false));
				}
			} catch (Exception ex) {
				Debug.LogException(ex);
			}
		}
	}
}
