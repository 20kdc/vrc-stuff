using System;
using System.IO;
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
		public abstract void BuildSearchOrder(AssetImportContext ctx, List<KDCBSPAbstractWorkspaceConfig> searchOrder);

		/// Sets up the workspace config search order.
		/// This version does not require AssetImportContext.
		/// DO NOT CALL THIS FUNCTION ; Call PrepareSearchOrderEditor instead (it adds the builtin workspace)
		public abstract void BuildSearchOrderEditor(List<KDCBSPAbstractWorkspaceConfig> searchOrder);

		/// Returns the fallback material.
		public abstract KDCBSPAbstractMaterialConfig FallbackMaterial(AssetImportContext ctx);

		/// Looks up a material in this config, or returns null.
		/// Note it does not check 'sub-configs' as covered in the search order.
		public abstract KDCBSPAbstractMaterialConfig LookupMaterial(AssetImportContext ctx, string path);

		/// Returns the fallback entity type.
		public abstract GameObject FallbackEntity(AssetImportContext ctx);

		/// Looks up an entity type in this config, or returns null.
		/// Note it does not check 'sub-configs' as covered in the search order.
		public abstract GameObject LookupEntity(AssetImportContext ctx, string classname);

		/// Contributes (overwriting) to a map from material names to their material paths and material configs.
		/// The idea is to get a full materials index as part of TrenchBroom material setup.
		public abstract void FindMaterials(SortedDictionary<string, (string, KDCBSPAbstractMaterialConfig)> materials);

		/// General 'find everything' function, called upon by SetupBaseQ2 among others.
		public virtual void FindEverything(out SortedDictionary<string, (string, KDCBSPAbstractMaterialConfig)> materials) {
			var lst = PrepareSearchOrderEditor();
			lst.Reverse();
			materials = new();
			foreach (var elm in lst) {
				elm.FindMaterials(materials);
			}
		}

		/// Sets up the baseq2 directory.
		public virtual void SetupBaseQ2() {
			string path = AssetDatabase.GetAssetPath(this);
			if (path == null) {
				Debug.LogError("No asset path for workspace being setup!");
			} else {
				string baseDir = Path.GetDirectoryName(path);
				if (baseDir == null) {
					Debug.LogError("No base dir for workspace being setup!");
				} else {
					string baseq2Dir = Path.Join(baseDir, "baseq2");
					try {
						Directory.CreateDirectory(FileUtil.GetPhysicalPath(baseq2Dir));
					} catch (Exception ex) {
						Debug.LogException(ex);
					}
					try {
						FindEverything(out var materials);
						SortedDictionary<string, byte[]> files = new();
						foreach (var (key, value) in materials)
							value.Item2.PAKContribute(files, key, value.Item1);
						KDCBSPUtilities.UpdatePAKFile(FileUtil.GetPhysicalPath(baseq2Dir), KDCBSPPK3Writer.MakePK3(files));
					} catch (Exception ex) {
						Debug.LogException(ex);
					}
				}
			}
		}

		/// Prepares a finished search order.
		public List<KDCBSPAbstractWorkspaceConfig> PrepareSearchOrder(AssetImportContext ctx) {
			List<KDCBSPAbstractWorkspaceConfig> searchOrder = new();
			searchOrder.Add(this);
			BuildSearchOrder(ctx, searchOrder);

			var builtInWorkspace = KDCBSPImportContext.DependsOnArtifact<KDCBSPAbstractWorkspaceConfig>(ctx, KDCBSPUtilities.KVBSP_BASE + "Assets/builtinWorkspace.asset");
			if (builtInWorkspace != null)
				searchOrder.Add(builtInWorkspace);

			return searchOrder;
		}

		/// Prepares a finished search order (for use in non-importer code)
		public List<KDCBSPAbstractWorkspaceConfig> PrepareSearchOrderEditor() {
			List<KDCBSPAbstractWorkspaceConfig> searchOrder = new();
			searchOrder.Add(this);
			BuildSearchOrderEditor(searchOrder);

			var builtInWorkspace = (KDCBSPAbstractWorkspaceConfig) AssetDatabase.LoadAssetAtPath(KDCBSPUtilities.KVBSP_BASE + "Assets/builtinWorkspace.asset", typeof(KDCBSPAbstractWorkspaceConfig));
			if (builtInWorkspace != null)
				searchOrder.Add(builtInWorkspace);

			return searchOrder;
		}
	}
}
