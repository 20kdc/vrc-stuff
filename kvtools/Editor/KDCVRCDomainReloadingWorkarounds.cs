using System;
using System.IO;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEditor;
using UnityEditor.Callbacks;
using VRC.SDK3.ClientSim;
using VRC.Udon;
using VRC.Udon.ProgramSources;

/**
 * Contains 'domain reloading disabled' workarounds.
 * Also see: https://feedback.vrchat.com/sdk-bug-reports/p/udonsharp-watchlog-causing-extreme-long-reloading-domain-time-and-editor-stutter
 */
namespace KDCVRCTools {
	public class KDCVRCDomainReloadingWorkarounds {
		[InitializeOnLoadMethod]
		private static void DoSetup() {
			EditorApplication.playModeStateChanged += OnPlayModeChanged;
		}

		public static void OnPlayModeChanged(PlayModeStateChange state) {
			if (state == PlayModeStateChange.ExitingPlayMode) {
				// So I decided to look into this in case there was a better way.
				// Short answer: No-ish.
				// Long answer: We should use ExitingPlayMode because ClientSimEditorRuntimeLinker might be doing networking init as early as ExitingEditMode.
				// So in theory, it's better to just try and 'undo the damage'.
				// However, see OnSceneBuild.
				// Thanks to Pokerface Cactus from the Canny for the general idea, though this implementation and use of it is my own.
				FlushBrokenCaches();
			}
		}

		[PostProcessScene]
		private static void OnSceneBuild() {
			// Since scene build precedes ClientSim setup, it's essentially the perfect time to wipe all the caches.
			// This means we don't need to enter play mode twice for network calling metadata, for instance.
			FlushBrokenCaches();
		}

		public static void FlushBrokenCaches() {
			// Now, because we don't want to accidentally cripple things rather than fix them, try/catch is MANDATORY here.
			// If we get an exception, live with it!

			// -- PlayerObjects --
			// https://feedback.vrchat.com/persistence/p/clientsim-doesnt-work-with-player-objects-when-reload-domain-disabled
			try {
				typeof(ClientSimNetworkingUtilities).GetField("_playerObjectList", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.FlattenHierarchy).SetValue(null, null);
			} catch (Exception ex) {
				Debug.LogException(ex);
			}

			// -- NetworkCalling metadata stickiness --
			// This is a *lot* of SerializedUdonProgramAsset bugs.
			try {
				string[] udonGuids = AssetDatabase.FindAssets("t:SerializedUdonProgramAsset");
				foreach (var guid in udonGuids) {
					string assetPath = AssetDatabase.GUIDToAssetPath(guid);

					// If it's not loaded, we don't need to load it.
					if (!AssetDatabase.IsMainAssetAtPathLoaded(assetPath))
						continue;

					var supa = AssetDatabase.LoadAssetAtPath<SerializedUdonProgramAsset>(assetPath);
					if (supa == null)
						continue;

					// On success, erase all of the broken caches.
					var bindingFlags = BindingFlags.Instance | BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.FlattenHierarchy;
					typeof(SerializedUdonProgramAsset).GetField("_networkCallingEntrypointMetadataMap", bindingFlags).SetValue(supa, null);
					typeof(SerializedUdonProgramAsset).GetField("_entrypointHashToName", bindingFlags).SetValue(supa, new Dictionary<uint, string>());
					typeof(SerializedUdonProgramAsset).GetField("_entrypointNameToHash", bindingFlags).SetValue(supa, new Dictionary<string, uint>());
					typeof(SerializedUdonProgramAsset).GetField("_entrypointHashesLoaded", bindingFlags).SetValue(supa, 0);
				}
			} catch (Exception ex) {
				Debug.LogException(ex);
			}
		}
	}
}
