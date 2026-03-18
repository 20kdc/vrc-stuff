using UnityEngine;
using UnityEditor;
using UnityEditor.AssetImporters;
using System.IO;

namespace KDCVRCTools {
	[ScriptedImporter(1, "udonjson")]
	public class KDCJSONPrecompiledUdonAssetImporter : ScriptedImporter {
		public override void OnImportAsset(AssetImportContext ctx) {
			var asset = ScriptableObject.CreateInstance<KDCJSONPrecompiledUdonAsset>();

			var so = new SerializedObject(asset);
			so.FindProperty("InternalJSON").stringValue = File.ReadAllText(ctx.assetPath);
			so.ApplyModifiedProperties();

			asset.RefreshProgram();

			ctx.AddObjectToAsset("KDCVRC: Imported Udon JSON Program", asset);
			ctx.SetMainObject(asset);
		}
	}
}
