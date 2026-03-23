using System;
using System.IO;
using System.Reflection;
using VRC.Udon;
using VRC.Udon.Common.Interfaces;
using VRC.Udon.ProgramSources;
using VRC.Udon.Editor.ProgramSources;
using UnityEngine;
using UnityEditor;

namespace KDCVRCTools {
	[CustomEditor(typeof(KDCPinnedSUPAImporter))]
	public class KDCPinnedSUPAImporterEditor : Editor {
		private SerializedProperty _targetProperty;
		void OnEnable() {
			_targetProperty = serializedObject.FindProperty("target");
		}

		public override void OnInspectorGUI() {
			EditorGUILayout.PropertyField(_targetProperty);
			serializedObject.ApplyModifiedProperties();
			// other
			UdonProgramAsset upa = _targetProperty.objectReferenceValue as UdonProgramAsset;
			if (upa == null) {
				EditorGUILayout.HelpBox("Unable to resolve target as UdonProgramAsset", MessageType.Error);
				return;
			}
			AssetDatabase.TryGetGUIDAndLocalFileIdentifier(upa, out string upaGuid, out long _);
			EditorGUILayout.TextField("Target GUID", upaGuid);
			KDCPinnedSUPAImporter kpsi = (KDCPinnedSUPAImporter) serializedObject.targetObject;
			if (kpsi == null) {
				EditorGUILayout.HelpBox("Couldn't resolve KDCPinnedSUPAImporter", MessageType.Error);
				return;
			}
			var imported = (SerializedUdonProgramAsset) AssetDatabase.LoadAssetAtPath(kpsi.assetPath, typeof(SerializedUdonProgramAsset));
			if (imported == null) {
				EditorGUILayout.HelpBox("Couldn't resolve import", MessageType.Error);
				return;
			}
			AssetDatabase.TryGetGUIDAndLocalFileIdentifier(imported, out string importedGuid, out long _);
			EditorGUILayout.TextField("Import Cache GUID", importedGuid);
			if (imported.name != upaGuid) {
				EditorGUILayout.HelpBox("GUID of target must match name, or it auto-unlinks", MessageType.Error);
				return;
			}
			if (GUILayout.Button("Link/Refresh")) {
				try {
					upa.GetType().GetField("serializedUdonProgramAsset", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance | BindingFlags.FlattenHierarchy).SetValue(upa, imported);
					upa.RefreshProgram();
				} catch (Exception ex) {
					Debug.LogException(ex);
				}
			}
		}
	}
}
