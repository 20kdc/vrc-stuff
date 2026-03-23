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

		public static int SetupDeterministicSUPAGUIDsCore() {
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
	}
}
