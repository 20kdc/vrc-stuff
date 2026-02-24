using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using VRC.Compression;
using VRC.Udon;
using VRC.Udon.Common.Interfaces;
using VRC.Udon.ProgramSources;
using VRC.Udon.Editor.ProgramSources;
using VRC.SDK3.UdonNetworkCalling;
using VRC.SDK3.Data;
using VRC.Udon.Editor;
using VRC.Udon.Serialization.OdinSerializer;
using VRC.Udon.Serialization.OdinSerializer.Utilities;

namespace KDCVRCTools {
	/**
	 * So after the whole signatures thing made it pretty clear a direct translation wasn't going to work too well, I decided on a new approach.
	 * The plan is pretty simple: We keep a SerializedUdonProgramAsset in JSON form.
	 * That's the whole plan.
	 * ...ok, the plan ran into significant problems on the reading side because... Unity.
	 * Unity is being absolutely awful at every step of the way.
	 * One would think the editor JSON serialization able to carry UUIDs would properly deserialize.
	 * It either doesn't, or the upcoming stuff makes it disappear, or something.
	 * (The code might get reverted to remove BunnyRegular if it's not actually useful.)
	 * So we spend a lot of effort essentially just to get back where we started.
	 * ...and then, of course, the deserialized public variables **still** regularly disappear into the aether!
	 * I'm not sure how -- the diffs are showing it's perfectly replicating the original file.
	 * I think you'll just have to live with this.
	 */
	public class KDCJSONPrecompiledUdonAsset : UdonProgramAsset {

		[SerializeField]
		public string InternalJSON = "{}";

		// basically something is being incredibly painful on what will and won't deserialize
		public KDCJSONLittleFakeProxyObject BunnyEditor;
		public DataToken BunnyRegular;

		protected override void DrawProgramSourceGUI(UdonBehaviour udonBehaviour, ref bool dirty) {
			if(GUILayout.Button("Refresh Program"))
				UdonEditorManager.Instance.QueueAndRefreshProgram(this);
			DrawInteractionArea(udonBehaviour);
			DrawPublicVariables(udonBehaviour, ref dirty);
			DrawProgramDisassembly();
		}

		protected override void RefreshProgramImpl() {
			if (BunnyEditor != null) {
				UnityEngine.Object.DestroyImmediate(BunnyEditor);
				BunnyEditor = null;
			}
			program = null;
			BunnyEditor = ScriptableObject.CreateInstance<KDCJSONLittleFakeProxyObject>();
			BunnyRegular = ScriptableObject.CreateInstance<KDCJSONLittleFakeProxyObject>();
			// This is so stupid; the files won't resolve right.
			EditorJsonUtility.FromJsonOverwrite(InternalJSON, BunnyEditor);
			if (!VRCJson.TryDeserializeFromJson(InternalJSON, out BunnyRegular)) {
				Debug.Log("Invalid JSON in KDCJSONPrecompiledUdonAsset maybe");
			}
			// patchup
			List<UnityEngine.Object> uoes = new();
			BunnyRegular.DataDictionary.TryGetValue("MonoBehaviour", out DataToken monoBehaviourJson);
			if (monoBehaviourJson.TokenType == TokenType.DataDictionary) {
				monoBehaviourJson.DataDictionary.TryGetValue("programUnityEngineObjects", out DataToken programUnityEngineObjects);
				if (programUnityEngineObjects.TokenType == TokenType.DataList) {
					foreach (DataToken fr in programUnityEngineObjects.DataList) {
						uoes.Add(ResolveReference(fr));
					}
				} else {
					Debug.Log("programUnityEngineObjects missing presumed taken by space narwhals, " + BunnyRegular);
				}
			} else {
				Debug.Log("json.MonoBehaviour missing presumed taken by sky dragons, " + BunnyRegular);
			}
			// done
			program = BunnyEditor.RetrieveProgram(uoes);
		}

		protected override NetworkCallingEntrypointMetadata[] GetLastNetworkCallingMetadata() {
			if (BunnyEditor == null)
				return null;
			return BunnyEditor.networkCallingEntrypointMetadata;
		}

		public static string DataTokenToJSON(DataToken f) {
			if (VRCJson.TrySerializeToJson(f, JsonExportType.Minify, out DataToken result))
				return result.String;
			return "(failed serialize somehow)";
		}

		public static UnityEngine.Object ResolveReference(DataToken falseRef) {
			if (falseRef.TokenType != TokenType.DataDictionary) {
				Debug.Log("KDCVRCFakeReference is not dictionary: " + DataTokenToJSON(falseRef) + ", it says it's " + falseRef.TokenType);
				return null;
			}
			falseRef.DataDictionary.TryGetValue("guid", out DataToken guidDT);
			falseRef.DataDictionary.TryGetValue("fileID", out DataToken fileIDDT);
			if (guidDT.TokenType != TokenType.String) {
				Debug.Log("KDCVRCFakeReference guid not string: " + DataTokenToJSON(falseRef));
				return null;
			}
			if (!fileIDDT.IsNumber) {
				Debug.Log("KDCVRCFakeReference fileID not number: " + DataTokenToJSON(falseRef));
				return null;
			}
			var guid = guidDT.String;
			// this is so hacky...
			if (fileIDDT.TokenType == TokenType.Double)
				fileIDDT = (long) fileIDDT.Double;
			var fileID = fileIDDT.Long;

			var path = AssetDatabase.GUIDToAssetPath(guid);
			if (path != null) {
				foreach (UnityEngine.Object obj in AssetDatabase.LoadAllAssetsAtPath(path)) {
					if (AssetDatabase.TryGetGUIDAndLocalFileIdentifier(obj, out var objGuid, out long objLocalID)) {
						if (objGuid == guid && objLocalID == fileID) {
							// Debug.Log("FalseReference resolved successfully to " + obj);
							return obj;
						}
					}
				}
				Debug.Log("KDCVRCFakeReference no object: " + DataTokenToJSON(falseRef));
			} else {
				Debug.Log("KDCVRCFakeReference no file: " + DataTokenToJSON(falseRef));
			}
			return null;
		}

		public class KDCJSONLittleFakeProxyObject : ScriptableObject {
			[SerializeField]
			public byte[] serializedProgramCompressedBytes;
			[SerializeField]
			public NetworkCallingEntrypointMetadata[] networkCallingEntrypointMetadata;

			public IUdonProgram RetrieveProgram(List<UnityEngine.Object> uoes) {
				return VRC.Udon.Serialization.OdinSerializer.SerializationUtility.DeserializeValue<IUdonProgram>(GZip.Decompress(serializedProgramCompressedBytes), DataFormat.Binary, uoes);
			}
		}
	}
}
