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
				((KDCBSPAbstractWorkspaceConfig) target).FindEverything(out var materials);
				foreach (var (key, value) in materials) {
					Debug.Log((key, value.Item1));
				}
			}

			if (GUILayout.Button("Setup 'baseq2'")) {
				((KDCBSPAbstractWorkspaceConfig) target).SetupBaseQ2();
			}
			GUILayout.Label("Setup 'baseq2' on your game root workspace when adding/removing/changing materials!", EditorStyles.wordWrappedLabel);

			serializedObject.ApplyModifiedProperties();
		}
	}
}
