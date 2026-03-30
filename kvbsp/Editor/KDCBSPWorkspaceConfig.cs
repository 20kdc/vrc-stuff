using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEditor;
using UnityEditor.AssetImporters;

namespace KDCVRCBSP {
	[CreateAssetMenu(menuName = "KDCVRCTools/KDCBSP Workspace Config", fileName = "kdcbspWorkspaceConfig")]
	public class KDCBSPWorkspaceConfig : KDCBSPAbstractWorkspaceConfig {
		// [TRANSFORM]
		// This is calibrated to vr_player_stick
		[Tooltip("How many Quake units map to 1 metre?")]
		[SerializeField]
		public float worldScale = 64.0f;

		[Tooltip("Material configs are stored in this (relative) directory.")]
		[SerializeField]
		public string materialConfigsPath = "textures";

		[Tooltip("Entity configs are stored in this (relative) directory.")]
		[SerializeField]
		public string entityConfigsPath = "progs";

		[Tooltip("Fallback material (used if none found).")]
		[SerializeField]
		public LazyLoadReference<KDCBSPAbstractMaterialConfig> fallbackMaterial;

		[Tooltip("Fallback entity (used if none found).")]
		[SerializeField]
		public LazyLoadReference<GameObject> fallbackEntity;

		public override float WorldScale => worldScale;

		[Tooltip("Parent workspaces can be used to reuse different workspaces in each other.")]
		[SerializeField]
		public List<LazyLoadReference<KDCBSPAbstractWorkspaceConfig>> parentWorkspaces = new();

		public override void BuildSearchOrder(AssetImportContext ctx, List<KDCBSPAbstractWorkspaceConfig> searchOrder) {
			foreach (var workspace in parentWorkspaces) {
				KDCBSPAbstractWorkspaceConfig ws = KDCBSPImportContext.DependsOnArtifact(ctx, workspace);
				if (ws != null) {
					if (searchOrder.Contains(ws))
						continue;
					searchOrder.Add(ws);
					ws.BuildSearchOrder(ctx, searchOrder);
				}
			}
		}

		public override KDCBSPAbstractMaterialConfig FallbackMaterial(AssetImportContext ctx) {
			KDCBSPAbstractMaterialConfig fbc = KDCBSPImportContext.DependsOnArtifact(ctx, fallbackMaterial);
			if (fbc == null) {
				fbc = KDCBSPImportContext.DependsOnArtifact<KDCBSPAbstractMaterialConfig>(ctx, "Packages/t20kdc.vrc-bsp/Assets/missingMaterial.asset");
				fallbackMaterial = fbc;
				return fbc;
			}
			return fbc;
		}

		public override KDCBSPAbstractMaterialConfig LookupMaterial(AssetImportContext ctx, string path) {
			string assetPath = AssetDatabase.GetAssetPath(this);
			string baseDir = Path.GetDirectoryName(assetPath);
			if (baseDir == null)
				return null;
			string path1 = Path.Join(baseDir, materialConfigsPath, path + ".asset");
			string path2 = Path.Join(baseDir, materialConfigsPath, path + ".mat");
			KDCBSPAbstractMaterialConfig amc = KDCBSPImportContext.DependsOnArtifact<KDCBSPAbstractMaterialConfig>(ctx, path1);
			if (amc != null)
				return amc;
			Material fbc = KDCBSPImportContext.DependsOnArtifact<Material>(ctx, path2);
			if (fbc != null) {
				// Auto-create a material for ease of use.
				KDCBSPMaterialConfig cfg = (KDCBSPMaterialConfig) ScriptableObject.CreateInstance(typeof(KDCBSPMaterialConfig));
				cfg.material = fbc;
				return cfg;
			}
			return null;
		}

		public override GameObject FallbackEntity(AssetImportContext ctx) {
			GameObject fbc = KDCBSPImportContext.DependsOnArtifact(ctx, fallbackEntity);
			if (fbc == null) {
				fbc = KDCBSPImportContext.DependsOnArtifact<GameObject>(ctx, "Packages/t20kdc.vrc-bsp/Assets/missingEntity.prefab");
				fallbackEntity = fbc;
				return fbc;
			}
			return fbc;
		}

		public override GameObject LookupEntity(AssetImportContext ctx, string classname) {
			string assetPath = AssetDatabase.GetAssetPath(this);
			string baseDir = Path.GetDirectoryName(assetPath);
			if (baseDir == null)
				return null;
			string path1 = Path.Join(baseDir, entityConfigsPath, classname + ".prefab");
			GameObject amc = KDCBSPImportContext.DependsOnArtifact<GameObject>(ctx, path1);
			if (amc != null)
				return amc;
			return null;
		}
	}
}
