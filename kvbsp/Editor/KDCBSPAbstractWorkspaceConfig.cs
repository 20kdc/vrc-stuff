using System;
using System.IO;
using System.Text;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEditor;
using UnityEditor.AssetImporters;

namespace KDCVRCBSP {
	/**
	 * KDCBSPAbstractWorkspaceConfig defines everything a workspace config is queried for.
	 */
	public abstract class KDCBSPAbstractWorkspaceConfig : ScriptableObject {
		/// How many Quake units map to 1 metre?
		public abstract float WorldScale { get; }

		/// Sets up the workspace config search order. This does not include this workspace config.
		/// Duplicates are not allowed. This function is expected to be recursive.
		/// DO NOT CALL THIS FUNCTION ; Call PrepareSearchOrder instead (it adds the builtin workspace)
		public abstract void BuildSearchOrder(IKDCBSPAssetContext ctx, List<KDCBSPAbstractWorkspaceConfig> searchOrder);

		/// Returns the fallback material.
		public abstract KDCBSPAbstractMaterialConfig FallbackMaterial(IKDCBSPAssetContext ctx);

		/// Looks up a material in this config, or returns null.
		/// Note it does not check 'sub-configs' as covered in the search order.
		public abstract KDCBSPAbstractMaterialConfig LookupMaterial(IKDCBSPAssetContext ctx, string path);

		/// Returns the fallback entity type.
		public abstract GameObject FallbackEntity(IKDCBSPAssetContext ctx);

		/// Looks up an entity type in this config, or returns null.
		/// Note it does not check 'sub-configs' as covered in the search order.
		public abstract GameObject LookupEntity(IKDCBSPAssetContext ctx, string classname);

		/// Contributes (overwriting) to a map from material names to their material paths and material configs.
		/// The idea is to get a full materials index as part of TrenchBroom material setup.
		public abstract void FindMaterials(SortedDictionary<string, (string, KDCBSPAbstractMaterialConfig)> materials);
		public abstract void FindEntities(SortedDictionary<string, GameObject> entities);

		/// General 'find everything' function, called upon by SetupBaseQ2 among others.
		public virtual void FindEverything(out SortedDictionary<string, (string, KDCBSPAbstractMaterialConfig)> materials, out SortedDictionary<string, GameObject> entities) {
			var lst = PrepareSearchOrder(new KDCBSPNonImportAssetContext());
			lst.Reverse();
			materials = new();
			entities = new();
			foreach (var elm in lst) {
				elm.FindMaterials(materials);
				elm.FindEntities(entities);
			}
		}

		/// Sets up the VFS directory.
		public virtual void SetupVFS() {
			string path = AssetDatabase.GetAssetPath(this);
			if (path == null) {
				Debug.LogError("No asset path for workspace being setup!");
			} else {
				string baseDir = Path.GetDirectoryName(path);
				if (baseDir == null) {
					Debug.LogError("No base dir for workspace being setup!");
				} else {
					try {
						var encoding = new UTF8Encoding(false);
						FindEverything(out var materials, out var entities);
						SortedDictionary<string, byte[]> files = new();
						string shaderFile = "";
						foreach (var (key, value) in materials) {
							value.Item2.PAKContribute(files, key, value.Item1);
							if (!key.Contains(" ")) {
								string q3shader = value.Item2.PAKGetQ3Shader(key, value.Item1);
								if (q3shader != "") {
									// needs 'textures/' prefix or Radiant believes it doesn't exist :<
									shaderFile += "\ntextures/" + key + " {\n";
									shaderFile += q3shader;
									shaderFile += "\n}\n";
								}
							}
						}
						files["scripts/kvbspGen.shader"] = encoding.GetBytes(shaderFile);
						files["scripts/shaderlist.txt"] = encoding.GetBytes("kvbspGen\n");
						var baseDirPhys = FileUtil.GetPhysicalPath(baseDir);

						KDCBSPEntityDescriptor.ExtractAll(entities, out var fgdText, out var entText);

						KDCBSPUtilities.UpdateVFS(baseDirPhys, files, fgdText, entText);
					} catch (Exception ex) {
						Debug.LogException(ex);
					}
				}
			}
		}

		/// Prepares a finished search order.
		public List<KDCBSPAbstractWorkspaceConfig> PrepareSearchOrder(IKDCBSPAssetContext ctx) {
			List<KDCBSPAbstractWorkspaceConfig> searchOrder = new();
			searchOrder.Add(this);
			BuildSearchOrder(ctx, searchOrder);

			var builtInWorkspace = ctx.DependsOnArtifact<KDCBSPAbstractWorkspaceConfig>(KDCBSPUtilities.KVBSP_BASE + "Assets/builtinWorkspace.asset");
			if (builtInWorkspace != null)
				searchOrder.Add(builtInWorkspace);

			return searchOrder;
		}
	}
}
