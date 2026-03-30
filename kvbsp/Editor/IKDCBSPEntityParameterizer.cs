using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEditor;
using UnityEditor.AssetImporters;

namespace KDCVRCBSP {
	/**
	 * This is a MonoBehaviour put on the root of an entity prefab.
	 * If not found, the entity is compiled with the same logic worldspawn is compiled with, which can be found in KDCBSPImporter.
	 */
	public interface IKDCBSPEntityParameterizer {
		/**
		 * Compiles the entity into this GameObject.
		 * This has carte blanche ability to do, basically, 'whatever it wants to'.
		 */
		public void Compile(KDCBSPImportContext ctx, KDCBSPIntermediate.Entity entity, string assetPrefix, bool isWorldspawn);
	}
}
