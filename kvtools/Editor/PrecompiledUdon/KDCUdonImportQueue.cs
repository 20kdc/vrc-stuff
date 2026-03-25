using UnityEngine;
using UnityEditor;
using UnityEditor.AssetImporters;
using VRC.Udon;
using VRC.Udon.ProgramSources;
using VRC.Udon.Editor;
using System.IO;
using System.Collections.Generic;

namespace KDCVRCTools {
	/**
	 * UdonEditorManager's queue doesn't work from importers, so this is our own queue that does and then passes the result to UEM's queue.
	 */
	public class KDCUdonImportQueue {
		private static HashSet<string> AssetPaths = new();

		/**
		 * Queues an asset by path.
		 */
		public static void Queue(string s) {
			AssetPaths.Add(s);
			EditorApplication.update += Callback;
		}

		public static void Callback() {
			/* let's avoid UdonSharp's mistake */
			EditorApplication.update -= Callback;
			if (AssetPaths.Count == 0)
				return;
			// Debug.Log("KDCUdonImportQueue fired");
			foreach (string path in AssetPaths) {
				var res = AssetDatabase.LoadAssetAtPath<AbstractUdonProgramSource>(path);
				if (res != null) {
					UdonEditorManager.Instance.CancelQueuedProgramSourceRefresh(res);
					UdonEditorManager.Instance.QueueProgramSourceRefresh(res);
				}
			}
			AssetPaths.Clear();
			UdonEditorManager.Instance.RefreshQueuedProgramSources();
		}
	}
}
