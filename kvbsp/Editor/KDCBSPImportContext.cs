using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEditor;
using UnityEditor.AssetImporters;

namespace KDCVRCBSP {
	/**
	 * Contains import context info.
	 */
	public struct KDCBSPImportContext {
		public const string KVBSP_BASE = "Packages/t20kdc.vrc-bsp/";

		public KDCBSPImporter importer;
		public KDCBSPAbstractWorkspaceConfig workspace;
		public List<KDCBSPAbstractWorkspaceConfig> searchOrder;
		public KDCBSPIntermediate bsp;
		public AssetImportContext assetImportContext;
		public Dictionary<String, KDCBSPAbstractMaterialConfig> materialCache;
		public Dictionary<String, GameObject> entityCache;

		public KDCBSPAbstractMaterialConfig LookupMaterial(string material) {
			if (materialCache.ContainsKey(material))
				return materialCache[material];
			KDCBSPAbstractMaterialConfig res;
			foreach (var cfg in searchOrder) {
				res = cfg.LookupMaterial(assetImportContext, material);
				if (res != null) {
					materialCache[material] = res;
					return res;
				}
			}
			res = workspace.FallbackMaterial(assetImportContext);
			materialCache[material] = res;
			return res;
		}

		public GameObject LookupEntity(string entity) {
			if (entityCache.ContainsKey(entity))
				return entityCache[entity];
			GameObject res;
			foreach (var cfg in searchOrder) {
				res = cfg.LookupEntity(assetImportContext, entity);
				if (res != null) {
					entityCache[entity] = res;
					return res;
				}
			}
			res = workspace.FallbackEntity(assetImportContext);
			entityCache[entity] = res;
			return res;
		}

		/// DependsOnArtifact and loader.
		public static T DependsOnArtifact<T>(AssetImportContext ctx, LazyLoadReference<T> obj) where T: UnityEngine.Object {
			T r = obj.asset;
			if (r != null) {
				string path = AssetDatabase.GetAssetPath(r);
				if (path != null)
					if (ctx != null)
						ctx.DependsOnArtifact(path);
			}
			return r;
		}

		/// DependsOnArtifact and loader.
		public static T DependsOnArtifact<T>(AssetImportContext ctx, string path) where T: UnityEngine.Object {
			// Notably, we setup the dependency regardless of if it's actually there.
			// This is intentional.
			if (ctx != null)
				ctx.DependsOnArtifact(path);
			return (T) AssetDatabase.LoadAssetAtPath(path, typeof(T));
		}
	}
}
