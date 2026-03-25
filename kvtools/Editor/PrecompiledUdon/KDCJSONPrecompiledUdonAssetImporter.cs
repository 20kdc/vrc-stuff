using UnityEngine;
using UnityEditor;
using UnityEditor.AssetImporters;
using VRC.Udon.Editor;
using System.IO;

namespace KDCVRCTools {
	[ScriptedImporter(1, "udonjson")]
	public class KDCJSONPrecompiledUdonAssetImporter : ScriptedImporter {
		public override void OnImportAsset(AssetImportContext ctx) {
			var asset = ScriptableObject.CreateInstance<KDCJSONPrecompiledUdonAsset>();
			asset.InternalJSON = File.ReadAllText(ctx.assetPath);

			// Debug.Log("**actually** imported: " + ctx.assetPath);
			// So here's 'the deal'. The UEM queue is mostly useless, especially in importers.
			// But we can use our own queue that actually works.
			KDCUdonImportQueue.Queue(ctx.assetPath);

			ctx.AddObjectToAsset("KDCVRC: Imported Udon JSON Program", asset);
			ctx.SetMainObject(asset);
		}
	}
}
