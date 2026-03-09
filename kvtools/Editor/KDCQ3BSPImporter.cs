using UnityEngine;
using UnityEditor;
using UnityEditor.AssetImporters;
using System.IO;

namespace KDCVRCTools {
	[ScriptedImporter(1, "bsp")]
	public class KDCQ3BSPImporter : ScriptedImporter {
		public override void OnImportAsset(AssetImportContext ctx) {
			GameObject mapGO = new GameObject("map");
			ctx.AddObjectToAsset("main obj", mapGO);
			ctx.SetMainObject(mapGO);
		}
	}
}
