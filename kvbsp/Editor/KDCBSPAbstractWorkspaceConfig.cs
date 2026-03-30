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
		public abstract void BuildSearchOrder(AssetImportContext ctx, List<KDCBSPAbstractWorkspaceConfig> searchOrder);

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
	}
}
