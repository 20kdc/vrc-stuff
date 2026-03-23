using System;
using System.IO;
using System.Collections.Generic;
using VRC.Udon;
using VRC.Udon.Common.Interfaces;
using VRC.Udon.ProgramSources;
using VRC.Udon.Editor.ProgramSources;
using UnityEngine;
using UnityEditor;
using UnityEditor.AssetImporters;

namespace KDCVRCTools {
	[ScriptedImporter(1, "pinnedsupa")]
	public class KDCPinnedSUPAImporter : ScriptedImporter {
		public LazyLoadReference<UdonProgramAsset> target;

		public override void OnImportAsset(AssetImportContext ctx) {
			var res = ScriptableObject.CreateInstance(typeof(SerializedUdonProgramAsset));
			ctx.AddObjectToAsset("main obj", res);
			ctx.SetMainObject(res);
		}
	}
}
