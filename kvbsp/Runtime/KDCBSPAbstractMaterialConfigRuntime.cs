using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEditor;
using UnityEditor.AssetImporters;

namespace KDCVRCBSP {
	/**
	 * This type should only ever be extended by KDCBSPAbstractMaterialConfig in the editor assembly.
	 * This type allows Runtime assemblies to hold references to editor material configs.
	 */
	public abstract class KDCBSPAbstractMaterialConfigRuntime : ScriptableObject {
	}
}
