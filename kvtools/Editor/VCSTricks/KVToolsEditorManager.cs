using System;
using System.IO;
using System.Collections.Generic;
using System.Text;
using VRC.Udon;
using VRC.Udon.Editor;
using VRC.Udon.ProgramSources;
using UnityEngine;
using UnityEditor;
using UnityEditor.AssetImporters;

namespace KDCVRCTools {
	public class KVToolsEditorManager {
		public static string DeterministicSUPAGUIDTransform(string programSourceGUIDStr) {
			if (!GUID.TryParse(programSourceGUIDStr, out GUID programSourceGUID)) {
				throw new Exception(programSourceGUIDStr + " was not valid to begin with.");
			}
			int adjustIdx = 0;
			uint res = Convert.ToUInt32(programSourceGUIDStr.Substring(adjustIdx, 8), 16);
			res ^= 0x4b564353;
			string backAdjUnpadded = Convert.ToString(res, 16);
			string backAdjPadded = backAdjUnpadded.PadLeft(8, '0');
			string finalized = programSourceGUIDStr.Substring(0, adjustIdx) + backAdjPadded + programSourceGUIDStr.Substring(adjustIdx + 8);
			if (!GUID.TryParse(finalized, out GUID check)) {
				throw new Exception("Converting " + programSourceGUIDStr + " to " + finalized + " resulted in an invalid GUID.");
			}
			return finalized;
		}

		public static void EnsureAllSUPAsExist() {
			string[] programSourceGUIDs = AssetDatabase.FindAssets("t:AbstractUdonProgramSource");
			foreach (string guid in programSourceGUIDs) {
				string assetPath = AssetDatabase.GUIDToAssetPath(guid);

				AbstractUdonProgramSource programSource = AssetDatabase.LoadAssetAtPath<AbstractUdonProgramSource>(assetPath);
				if (programSource == null)
					continue;

				// side-effects only
				SerializedUdonProgramAsset supa = programSource.SerializedProgramAsset as SerializedUdonProgramAsset;
			}
		}

		public static int SetupDeterministicSUPAGUIDsCore(bool dryRun = false, List<string> assetPaths = null) {
			int modified = 0;
			string[] programSourceGUIDs = AssetDatabase.FindAssets("t:AbstractUdonProgramSource");
			var encoding = new UTF8Encoding(false);
			foreach (string guid in programSourceGUIDs) {
				string assetPath = AssetDatabase.GUIDToAssetPath(guid);

				AbstractUdonProgramSource programSource = AssetDatabase.LoadAssetAtPath<AbstractUdonProgramSource>(assetPath);
				if (programSource == null)
					continue;

				SerializedUdonProgramAsset supa = programSource.SerializedProgramAsset as SerializedUdonProgramAsset;
				if (supa == null)
					continue;

				string expectedPath = Path.Combine("Assets", "SerializedUdonPrograms", $"{guid}.asset");

				var supaAtPath = (SerializedUdonProgramAsset) AssetDatabase.LoadAssetAtPath(
					expectedPath,
					typeof(SerializedUdonProgramAsset)
				);

				// Externalized SUPA. We can't mess with this safely!
				if (supa != supaAtPath)
					continue;

				try {
					string transformedGUID = DeterministicSUPAGUIDTransform(guid);
					AssetDatabase.TryGetGUIDAndLocalFileIdentifier(supa, out string supaGUID, out long _);
					if (supaGUID == transformedGUID)
						continue;

					if (dryRun) {
						if (assetPaths != null)
							assetPaths.Add(assetPath);
						modified++;
						continue;
					}

					// Target
					string expectedPathMeta = expectedPath + ".meta";
					string val = File.ReadAllText(expectedPathMeta, encoding);

					/*
					fileFormatVersion: 2
					guid: 458be65b909d475e080ad3750eb957ee
					NativeFormatImporter:
					*/

					int targettingIndex = val.IndexOf("\nguid: ");
					int targettingIndexEnd = targettingIndex + 7;
					if (targettingIndex == -1) {
						Debug.LogWarning("KDCVRCTools: Unable to perform metafile edit, no GUID line @ " + guid);
						continue;
					}
					int endTargetIndex = val.IndexOf("\n", targettingIndex + 1);
					if (endTargetIndex == -1)
						endTargetIndex = val.Length;
					string original = val.Substring(targettingIndexEnd, endTargetIndex - targettingIndexEnd);
					if (original == transformedGUID)
						continue;
					val = val.Substring(0, targettingIndexEnd) + transformedGUID + val.Substring(endTargetIndex);

					File.WriteAllText(expectedPathMeta, val, encoding);
					if (assetPaths != null)
						assetPaths.Add(assetPath);
					modified++;
				} catch (Exception ex) {
					Debug.LogError("KDCVRCTools: @ " + guid);
					Debug.LogException(ex);
				}
			}
			return modified;
		}

		[MenuItem("VRChat SDK/Utilities/Re-compile + Deterministic GUIDs (KDCVRCTools)")]
		public static void SetupDeterministicSUPAGUIDsAndRecompileAll() {
			EnsureAllSUPAsExist();
			AssetDatabase.SaveAssets();
			int modified = SetupDeterministicSUPAGUIDsCore();
			Debug.Log($"KDCVRCTools: Adjusted {modified} SUPA GUIDs for determinism.");
			AssetDatabase.Refresh();
			UdonEditorManager.RecompileAllProgramSources();
		}

		public static bool DomainReloadingWorkaroundsInstalled() {
			try {
				Type workaroundsType = Type.GetType("KDCVRCWorkarounds.KDCVRCDomainReloadingWorkarounds, KDCVRCWorkarounds");
				return true;
			} catch (Exception ex) {
				// do absolutely nothing!
				Exception ignored = (Exception) ex;
			}
			return false;
		}

		[MenuItem("VRChat SDK/KDCVRCTools/Detect Known Issues")]
		public static void DetectKnownIssues() {
			bool foundIssues = false;
			string info = "";
			// scan for non-deterministic GUIDs
			List<string> assetPaths = new();
			SetupDeterministicSUPAGUIDsCore(true, assetPaths);
			foreach (string assetPath in assetPaths) {
				foundIssues = true;
				info += $"\nProgramSource {assetPath} uses a non-deterministic GUID. This will cause GUID thrashing in version control. Use VRChat SDK/Utilities/Re-compile + Deterministic GUIDs (KDCVRCTools) to fix.";
			}
			// check for dodgy editor settings
			if (!EditorSettings.serializeInlineMappingsOnOneLine) {
				foundIssues = true;
				info += $"\nEditorSettings.serializeInlineMappingsOnOneLine is false. This causes issues in version control. Fix in Edit -> Project Settings -> Project -> Editor -> Asset Serialization.";
			}
			// stop UdonSharp from breaking everything
			Type udonSharpSettingsType = Type.GetType("UdonSharpEditor.UdonSharpSettings, UdonSharp.Editor");
			object udonSharpSettings = udonSharpSettingsType.GetMethod("GetSettings").Invoke(null, null);
			if (udonSharpSettings != null) {
				bool val1 = (bool) udonSharpSettingsType.GetField("listenForVRCExceptions").GetValue(udonSharpSettings);
				int val2 = (int) udonSharpSettingsType.GetField("watcherMode").GetValue(udonSharpSettings);
				// we don't turn these into issues because they don't affect all users
				if (val1) {
					info += "\nUdonSharp is attempting to listen for VRChat exceptions. This can cause hard-to-debug and extremely painful memory leaks for some users. Primary symptom is long Domain Reloading or crashing. Fix in Edit -> Project Settings -> Project -> Udon Sharp -> Debugging -> Listen for client exceptions";
				}
				if (val2 != 0) {
					info += "\nUdonSharp's log watcher is enabled. This can cause hard-to-debug and extremely painful memory leaks for some users. Primary symptom is long Domain Reloading or crashing. Fix in Edit -> Project Settings -> Project -> Udon Sharp -> Debugging -> Output log watch mode";
				}
			}
			// is Domain Reloading disabled?
			if (EditorSettings.enterPlayModeOptionsEnabled && ((EditorSettings.enterPlayModeOptions & EnterPlayModeOptions.DisableDomainReload) != 0)) {
				if (!DomainReloadingWorkaroundsInstalled()) {
					foundIssues = true;
					info += $"\nProject -> Editor -> Enter Play Mode Settings -> Reload Domain has been disabled and no known workaround package is installed. This will cause issues with Udon Network Events and will cause the existence of PlayerObjects to cause ClientSim to completely fail. Either install KVWorkarounds or find another solution and tell me about it so I can add detection for that.";
				}
			}
			// finish
			if (foundIssues) {
				Debug.LogWarning("KDCVRCTools: Issues detected!" + info);
			} else {
				Debug.Log("KDCVRCTools: No known issues! Advisories:" + info);
			}
		}
	}
}
