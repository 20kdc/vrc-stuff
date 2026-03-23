using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using VRC.Udon.Common;
using VRC.Udon.Common.Interfaces;
using VRC.Udon.Security;
using VRC.Udon.Serialization.OdinSerializer;
using VRC.Udon.Serialization.OdinSerializer.Utilities;
using VRC.Udon.ProgramSources;

namespace KDCVRCTools {
	public static class DumpOdinJSON {
		[MenuItem("VRChat SDK/KDCVRCTools/Dump Odin")]
		public static void ExtractData() {
			// This is extremely bad code!
			string udondb = Path.Combine(Application.dataPath, "SerializedUdonPrograms");
			string uresdb = Path.Combine("Library", "KDCVRCDumpUdonJSON");
			Directory.CreateDirectory(uresdb);
			foreach (string file in Directory.GetFiles(udondb)) {
				string fn = Path.GetFileName(file);
				// Debug.Log(fn);
				if (fn.EndsWith(".asset")) {
					string assetPath = Path.Combine("Assets", "SerializedUdonPrograms", fn);
					// Debug.Log(asset);
					SerializedUdonProgramAsset asset = (SerializedUdonProgramAsset) AssetDatabase.LoadAssetAtPath(assetPath, typeof(SerializedUdonProgramAsset));
					if (asset != null) {
						IUdonProgram program = asset.RetrieveProgram();
						try {
							string unityJsonFile = Path.Combine(uresdb, fn + ".udonjson");
							File.WriteAllText(unityJsonFile, EditorJsonUtility.ToJson(asset));
						} catch (Exception ex) {
							Debug.Log(" at " + fn + " - ujson");
							Debug.LogError(ex);
						}
						try {
							string odinBinFile = Path.Combine(uresdb, fn + ".odin.bin");
							byte[] serializedBin = VRC.Udon.Serialization.OdinSerializer.SerializationUtility.SerializeValue(program, DataFormat.Binary, out List<UnityEngine.Object> serializedUEOBin);
							File.WriteAllBytes(odinBinFile, serializedBin);
						} catch (Exception ex) {
							Debug.Log(" at " + fn + " - bin");
							Debug.LogError(ex);
						}
						try {
							string odinJsonFile = Path.Combine(uresdb, fn + ".odin.json");
							byte[] serializedJson = VRC.Udon.Serialization.OdinSerializer.SerializationUtility.SerializeValue(program, DataFormat.JSON, out List<UnityEngine.Object> serializedUEOJson);
							File.WriteAllBytes(odinJsonFile, serializedJson);
						} catch (Exception ex) {
							Debug.Log(" at " + fn + " - json");
							Debug.LogError(ex);
						}
					}
				}
			}
		}
	}
}
