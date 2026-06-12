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

		/// Returns the materials base (or null)
		public string FullMaterialsBase {
			get {
				string assetPath = AssetDatabase.GetAssetPath(this);
				if (assetPath == null)
					return null;
				string baseDir = Path.GetDirectoryName(assetPath);
				if (baseDir == null)
					return null;
				return Path.Join(baseDir, materialConfigsPath);
			}
		}

		/// Returns the entities base (or null)
		public string FullEntitiesBase {
			get {
				string assetPath = AssetDatabase.GetAssetPath(this);
				if (assetPath == null)
					return null;
				string baseDir = Path.GetDirectoryName(assetPath);
				if (baseDir == null)
					return null;
				return Path.Join(baseDir, entityConfigsPath);
			}
		}

		public override void BuildSearchOrder(IKDCBSPAssetContext ctx, List<KDCBSPAbstractWorkspaceConfig> searchOrder) {
			foreach (var workspace in parentWorkspaces) {
				KDCBSPAbstractWorkspaceConfig ws = ctx.DependsOnArtifact(workspace);
				if (ws != null) {
					if (searchOrder.Contains(ws))
						continue;
					searchOrder.Add(ws);
					ws.BuildSearchOrder(ctx, searchOrder);
				}
			}
		}

		public override KDCBSPAbstractMaterialConfig FallbackMaterial(IKDCBSPAssetContext ctx) {
			KDCBSPAbstractMaterialConfig fbc = ctx.DependsOnArtifact(fallbackMaterial);
			if (fbc == null) {
				fbc = ctx.DependsOnArtifact<KDCBSPAbstractMaterialConfig>(KDCBSPUtilities.KVBSP_BASE + "Assets/missingMaterial.asset");
				fallbackMaterial = fbc;
				return fbc;
			}
			return fbc;
		}

		public override KDCBSPAbstractMaterialConfig LookupMaterial(IKDCBSPAssetContext ctx, string path) {
			string baseDir = FullMaterialsBase;
			if (baseDir == null)
				return null;
			string path1 = Path.Join(baseDir, path + ".asset");
			string path2 = Path.Join(baseDir, path + ".mat");
			KDCBSPAbstractMaterialConfig amc = ctx.DependsOnArtifact<KDCBSPAbstractMaterialConfig>(path1);
			if (amc != null)
				return amc;
			Material fbc = ctx.DependsOnArtifact<Material>(path2);
			if (fbc != null) {
				// Auto-create a material for ease of use.
				KDCBSPMaterialConfig cfg = (KDCBSPMaterialConfig) ScriptableObject.CreateInstance(typeof(KDCBSPMaterialConfig));
				cfg.material = fbc;
				return cfg;
			}
			return null;
		}

		public override GameObject FallbackEntity(IKDCBSPAssetContext ctx) {
			GameObject fbc = ctx.DependsOnArtifact(fallbackEntity);
			if (fbc == null) {
				fbc = ctx.DependsOnArtifact<GameObject>(KDCBSPUtilities.KVBSP_BASE + "Assets/missingEntity.prefab");
				fallbackEntity = fbc;
				return fbc;
			}
			return fbc;
		}

		public override GameObject LookupEntity(IKDCBSPAssetContext ctx, string classname) {
			string baseDir = FullEntitiesBase;
			if (baseDir == null)
				return null;
			string path1 = Path.Join(baseDir, classname + ".prefab");
			GameObject amc = ctx.DependsOnArtifact<GameObject>(path1);
			if (amc != null)
				return amc;
			return null;
		}

		private void Finder(SortedDictionary<string, string> target, string[] exts, string implicitBase, string explicitBase) {
			string implicitBasePhysical = FileUtil.GetPhysicalPath(implicitBase);
			List<string> listFiles = new();
			try {
				foreach (string s in Directory.EnumerateFileSystemEntries(implicitBasePhysical)) {
					string fileName = Path.GetFileName(s);
					string fullPath = Path.Join(implicitBase, fileName);
					if (Directory.Exists(fullPath)) {
						// Note the manual path join for the explicit base.
						// The explicit base is a Q2 material name, and should always be written exactly as [dir/]file; no leading slash or ending extension.
						Finder(target, exts, fullPath, explicitBase + fileName + "/");
					} else {
						listFiles.Add(fileName);
					}
				}
			} catch (Exception ex) {
				// this just means it can't be enumerated
				_ = ex;
			}
			// Need to ensure that earlier (higher-priority) extensions override later ones.
			for (int i = exts.Length - 1; i >= 0; i--) {
				string ext = exts[i];
				foreach (string fileName in listFiles) {
					if (fileName.EndsWith(ext)) {
						string fullPath = Path.Join(implicitBase, fileName);
						string noExt = fileName.Substring(0, fileName.Length - ext.Length);
						target[explicitBase + noExt] = fullPath;
					}
				}
			}
		}

		public override void FindMaterials(SortedDictionary<string, (string, KDCBSPAbstractMaterialConfig)> materials) {
			string fb = FullMaterialsBase;
			if (fb == null)
				return;
			SortedDictionary<string, string> tmp = new();
			Finder(tmp, new string[] {".asset", ".mat"}, fb, "");
			foreach (var (key, path) in tmp) {
				if (path.EndsWith(".mat")) {
					Material fbc = (Material) AssetDatabase.LoadAssetAtPath(path, typeof(Material));
					if (fbc == null)
						continue;
					KDCBSPMaterialConfig cfg = (KDCBSPMaterialConfig) ScriptableObject.CreateInstance(typeof(KDCBSPMaterialConfig));
					cfg.material = fbc;
					materials[key] = (path, cfg);
				} else {
					KDCBSPAbstractMaterialConfig fbc = (KDCBSPAbstractMaterialConfig) AssetDatabase.LoadAssetAtPath(path, typeof(KDCBSPAbstractMaterialConfig));
					if (fbc == null)
						continue;
					materials[key] = (path, fbc);
				}
			}
		}

		public override void FindEntities(SortedDictionary<string, GameObject> entities) {
			string implicitBase = FullEntitiesBase;
			if (implicitBase == null)
				return;
			SortedDictionary<string, string> tmp = new();

			string implicitBasePhysical = FileUtil.GetPhysicalPath(implicitBase);
			try {
				foreach (string s in Directory.EnumerateFileSystemEntries(implicitBasePhysical)) {
					string fileName = Path.GetFileName(s);
					string fullPath = Path.Join(implicitBase, fileName);
					if (!Directory.Exists(fullPath)) {
						if (fileName.EndsWith(".prefab")) {
							string noExt = fileName.Substring(0, fileName.Length - 7);

							GameObject fbc = (GameObject) AssetDatabase.LoadAssetAtPath(fullPath, typeof(GameObject));
							if (fbc == null)
								continue;
							entities[noExt] = fbc;
						}
					}
				}
			} catch (Exception ex) {
				// this just means it can't be enumerated
				_ = ex;
			}
		}
	}
}
