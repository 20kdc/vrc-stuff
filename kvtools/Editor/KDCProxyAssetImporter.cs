using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.AssetImporters;

namespace KDCVRCTools {
	[ScriptedImporter(1, "proxyasset")]
	public class KDCProxyAssetImporter : ScriptedImporter {
		public override void OnImportAsset(AssetImportContext ctx) {
			string path = File.ReadAllText(ctx.assetPath).Trim();
			path = Path.Join(Path.GetDirectoryName(ctx.assetPath), path);
			ctx.DependsOnArtifact(path);
			var res = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path);
			ctx.AddObjectToAsset("main obj", res);
			ctx.SetMainObject(res);
		}
	}
}
