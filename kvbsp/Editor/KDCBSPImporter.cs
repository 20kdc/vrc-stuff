using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.AssetImporters;
using KDCVRCBSP.ECL;

namespace KDCVRCBSP {
	[ScriptedImporter(1, "bsp")]
	public class KDCBSPImporter : KDCBSPBaseImporter {
		public override ECLBSPFile CompileToIntermediate(KDCBSPImportContext importContext, string assetPath) {
			return ECLBSPFile.Load(File.ReadAllBytes(assetPath));
		}
	}
}
