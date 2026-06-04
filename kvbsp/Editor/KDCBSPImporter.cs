using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.AssetImporters;

namespace KDCVRCBSP {
	[ScriptedImporter(1, "bsp")]
	public class KDCBSPImporter : KDCBSPBaseImporter {
		public override KDCBSPIntermediate CompileToIntermediate(KDCBSPImportContext importContext, string assetPath) {
			return KDCBSPIntermediate.Load(File.ReadAllBytes(assetPath), importContext.workspace.WorldScale);
		}
	}
}
