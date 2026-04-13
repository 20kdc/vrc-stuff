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
		bool debug = false;

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

			if (GUILayout.Button("Update Quake VFS")) {
				((KDCBSPAbstractWorkspaceConfig) target).SetupBaseQ2();
			}
			GUILayout.Label("Run this on your game root workspace when editing materials.", EditorStyles.wordWrappedLabel);
			GUILayout.Label("TrenchBroom may need to be restarted.", EditorStyles.wordWrappedLabel);

			if (debug = EditorGUILayout.Foldout(debug, "Debug")) {
				if (GUILayout.Button("Debug material search")) {
					((KDCBSPAbstractWorkspaceConfig) target).FindEverything(out var materials);
					foreach (var (key, value) in materials) {
						Debug.Log((key, value.Item1));
					}
				}
			}

			serializedObject.ApplyModifiedProperties();
		}
	}
}
