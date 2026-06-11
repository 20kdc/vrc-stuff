using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEditor;
using UnityEditor.AssetImporters;
using KDCVRCBSP.ECL;

namespace KDCVRCBSP {
	/**
	 * Contains import context info.
	 */
	public sealed class KDCBSPImportContext : IKDCBSPImportContext {
		public KDCBSPBaseImporter importer;
		public KDCBSPAbstractWorkspaceConfig workspace;
		public List<KDCBSPAbstractWorkspaceConfig> searchOrder;
		public ECLBSPFile bsp;
		public AssetImportContext assetImportContext;
		public Dictionary<String, KDCBSPAbstractMaterialConfig> materialCache = new();
		public Dictionary<String, GameObject> entityCache = new();

		public ECLBSPFile BSP => bsp;
		public float WorldScale => workspace.WorldScale;

		public KDCBSPAbstractMaterialConfig LookupMaterial(string material) {
			if (materialCache.ContainsKey(material))
				return materialCache[material];
			KDCBSPAbstractMaterialConfig res;
			foreach (var cfg in searchOrder) {
				res = cfg.LookupMaterial(this, material);
				if (res != null) {
					materialCache[material] = res;
					return res;
				}
			}
			res = workspace.FallbackMaterial(this);
			materialCache[material] = res;
			return res;
		}

		public GameObject LookupEntity(string entity) {
			if (entityCache.ContainsKey(entity))
				return entityCache[entity];
			GameObject res;
			foreach (var cfg in searchOrder) {
				res = cfg.LookupEntity(this, entity);
				if (res != null) {
					entityCache[entity] = res;
					return res;
				}
			}
			res = workspace.FallbackEntity(this);
			entityCache[entity] = res;
			return res;
		}

		public void AddObjectToAsset(string name, UnityEngine.Object asset) {
			if (assetImportContext != null)
				assetImportContext.AddObjectToAsset(name, asset);
		}

		/// DependsOnArtifact and loader.
		public T DependsOnArtifact<T>(LazyLoadReference<T> obj) where T: UnityEngine.Object {
			T r = obj.asset;
			if (r != null) {
				string path = AssetDatabase.GetAssetPath(r);
				if (path != null)
					if (assetImportContext != null)
						assetImportContext.DependsOnArtifact(path);
			}
			return r;
		}

		/// DependsOnArtifact and loader.
		public T DependsOnArtifact<T>(string path) where T: UnityEngine.Object {
			// Notably, we setup the dependency regardless of if it's actually there.
			// This is intentional.
			if (assetImportContext != null)
				assetImportContext.DependsOnArtifact(path);
			return (T) AssetDatabase.LoadAssetAtPath(path, typeof(T));
		}
	}
}
