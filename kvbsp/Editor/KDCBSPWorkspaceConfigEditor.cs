using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace KDCVRCBSP {
	[CustomEditor(typeof(KDCBSPWorkspaceConfig))]
	public class KDCBSPWorkspaceConfigEditor : Editor {
		SerializedProperty pWorldScale;
		SerializedProperty pMaterialConfigsPath;
		SerializedProperty pEntityConfigsPath;
		SerializedProperty pFallbackMaterial;
		SerializedProperty pFallbackEntity;
		SerializedProperty pParentWorkspaces;

		void OnEnable() {
			pWorldScale = serializedObject.FindProperty("worldScale");
			pMaterialConfigsPath = serializedObject.FindProperty("materialConfigsPath");
			pEntityConfigsPath = serializedObject.FindProperty("entityConfigsPath");
			pFallbackMaterial = serializedObject.FindProperty("fallbackMaterial");
			pFallbackEntity = serializedObject.FindProperty("fallbackEntity");
			pParentWorkspaces = serializedObject.FindProperty("parentWorkspaces");
		}

		public override void OnInspectorGUI() {
			EditorGUILayout.PropertyField(pWorldScale);
			EditorGUILayout.PropertyField(pMaterialConfigsPath);
			EditorGUILayout.PropertyField(pEntityConfigsPath);
			EditorGUILayout.PropertyField(pFallbackMaterial);
			EditorGUILayout.PropertyField(pFallbackEntity);
			EditorGUILayout.PropertyField(pParentWorkspaces);

			if (GUILayout.Button("Debug material search")) {
				var lst = KDCBSPImporter.PrepareSearchOrderEditor((KDCBSPAbstractWorkspaceConfig) target);
				lst.Reverse();
				SortedDictionary<string, string> materialIcons = new();
				foreach (var elm in lst) {
					elm.FindMaterials(materialIcons);
				}
				foreach (var elm in materialIcons) {
					Debug.Log(elm);
				}
			}

			serializedObject.ApplyModifiedProperties();
		}
	}
}
