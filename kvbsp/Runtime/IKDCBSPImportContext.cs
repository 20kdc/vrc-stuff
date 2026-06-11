using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using KDCVRCBSP.ECL;

namespace KDCVRCBSP {
	/**
	 * Import context universal between editor and runtime assemblies.
	 */
	public interface IKDCBSPImportContext : IKDCBSPAssetContext {
		/// BSP file being worked with.
		public ECLBSPFile BSP {
			get;
		}

		/// Workspace world scale.
		public float WorldScale {
			get;
		}

		/// Looks up a material, returning the material config.
		public KDCBSPAbstractMaterialConfig LookupMaterial(string material);

		/// Looks up an entity, returning the prefab.
		public GameObject LookupEntity(string entity);

		/// Adds an object to the asset being built. Be sure to ensure unique names.
		public void AddObjectToAsset(string name, UnityEngine.Object asset);
	}
}
