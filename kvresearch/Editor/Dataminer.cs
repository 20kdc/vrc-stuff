using System;
using System.IO;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using VRC.Udon.Graph;
using VRC.Udon.Graph.NodeRegistries; // OwO
using VRC.Udon.Common.Interfaces;
using VRC.Udon.Editor;
using VRC.Udon.EditorBindings;
using VRC.SDK3.Network;
using VRC.SDK3.Data;
using UdonSharp.Compiler.Udon;
using VRC.Udon.Serialization.OdinSerializer;

namespace KDCVRCTools {
	public class Dataminer {

		private static readonly BindingFlags idkfaBindingFlags = BindingFlags.Instance | BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.FlattenHierarchy;

		private Type compilerUdonInterface;
		private UdonEditorInterface editorInterfaceInstance;
		private Dictionary<string, string> builtinEventLookup;
		private Dictionary<string, ImmutableArray<(string, Type)>> builtinEventArgumentsLookup;
		private MethodInfo getUdonTypeName;

		private HashSet<Type> allDiscoveredTypes = new();

		/// The output file.
		/// This format is very... we'll affectionately call it a 'GML-style mess'.
		/// But this is fine; it gets converted to something much nicer in D2J.
		public List<string> total = new();

		public Dataminer() {
			compilerUdonInterface = Type.GetType("UdonSharp.Compiler.Udon.CompilerUdonInterface, UdonSharp.Editor");

			var cacheInit = compilerUdonInterface.GetMethod("CacheInit", idkfaBindingFlags);
			cacheInit.Invoke(null, new object[] {});

			editorInterfaceInstance = (UdonEditorInterface) compilerUdonInterface.GetField("_editorInterfaceInstance", idkfaBindingFlags).GetValue(null);
			builtinEventLookup = (Dictionary<string, string>) compilerUdonInterface.GetField("_builtinEventLookup", idkfaBindingFlags).GetValue(null);
			builtinEventArgumentsLookup = (Dictionary<string, ImmutableArray<(string, Type)>>) compilerUdonInterface.GetField("_builtinEventArgumentsLookup", idkfaBindingFlags).GetValue(null);
			getUdonTypeName = compilerUdonInterface.GetMethod("GetUdonTypeName", idkfaBindingFlags, null, new Type[] {typeof(Type)}, null);

		}

		public string GetUdonTypeName(Type type) {
			return (string) getUdonTypeName.Invoke(null, new object[] {type});
		}

		/// Discovers a type.
		/// This used to discover all base types, but now bases are being elided.
		public void DiscoverTypes(Type t) {
			if (t == null)
				return;
			allDiscoveredTypes.Add(t);
		}

		/// Discovers types in an Udon node definition.
		public void DiscoverTypes(UdonNodeDefinition und) {
			DiscoverTypes(und.type);
			foreach (UdonNodeParameter par in und.parameters)
				DiscoverTypes(par.type);
		}

		/// Adds an Udon node definition.
		public void AddRecordExtern(UdonNodeDefinition und) {
			if (!und.fullName.Contains(".")) {
				total.Add("DISCARD_EXT");
				total.Add(und.fullName);
				return;
			}
			total.Add("EXTERN");
			total.Add(und.fullName); // extern_id
			NodeRegistryUtilities.DefinitionInfo addedInfo;
			int blanksToWrite = 2;
			try {
				addedInfo = NodeRegistryUtilities.GetNodeDefinitionInfo(und);
				total.Add(addedInfo.definitionType.ToString()); // extern_deftype
				blanksToWrite--;
				if (addedInfo.info is MemberInfo memberInfo) {
					total.Add(GetUdonTypeName(memberInfo.DeclaringType)); // extern_declaring
					blanksToWrite--;
				}
			} catch {
			}
			if (blanksToWrite >= 2)
				total.Add(""); // extern_deftype
			if (blanksToWrite >= 1)
				total.Add(""); // extern_declaring
			total.Add(GetUdonTypeName(und.type)); // extern_type
			total.Add(und.parameters.Count.ToString()); // len(extern_parameters)
			foreach (UdonNodeParameter par in und.parameters) {
				total.Add(par.name); // p_name
				total.Add(GetUdonTypeName(par.type)); // p_type
				total.Add(par.parameterType.ToString()); // p_dir
			}
		}

		public void FindTypeBasesTarget(Type target, List<Type> res) {
			if (res.Contains(target))
				return;
			res.Add(target);
			FindTypeBases(target, res);
		}

		/// Finds all bases of a type.
		/// This is done in a reasonably consistent search order.
		/// Note that, because we cull omitted types, we have to include recursive bases here.
		public void FindTypeBases(Type t, List<Type> res) {
			if (t.BaseType != null)
				FindTypeBasesTarget(t.BaseType, res);
			foreach (Type it in t.GetInterfaces())
				FindTypeBasesTarget(it, res);
		}

		/// Add all discovered types as dataset records.
		/// This is then *followed* by base records.
		public void AddRecordsType() {
			foreach (Type t in allDiscoveredTypes) {
				total.Add("TYPE");
				string typeUdonName = GetUdonTypeName(t);
				total.Add(typeUdonName); // type_name
				total.Add(TwoWaySerializationBinder.Default.BindToName(t)); // type_odin_name
				total.Add(((int) VRCUdonSyncTypeConverter.TypeToUdonType(t)).ToString()); // type_sync_type
				if (t.IsInterface) {
					total.Add("INTERFACE"); // type_kind
				} else if (t.IsPrimitive) {
					total.Add("PRIMITIVE"); // type_kind
				} else if (t.IsArray) {
					total.Add("ARRAY"); // type_kind
				} else if (t.IsValueType) {
					total.Add("STRUCT"); // type_kind
				} else {
					total.Add("OBJECT"); // type_kind
				}
				// if enum, add values
				if (t.BaseType == typeof(System.Enum)) {
					foreach (object v in Enum.GetValues(t)) {
						total.Add("EVAL");
						total.Add(typeUdonName);
						total.Add(Enum.GetName(t, v));
						total.Add(Convert.ToInt64(v).ToString());
					}
				}
			}
			foreach (Type t in allDiscoveredTypes) {
				string typeUdonName = GetUdonTypeName(t);
				List<Type> allBaseTypes = new();
				FindTypeBases(t, allBaseTypes);
				foreach (Type it in allBaseTypes) {
					// We only add TYPEBASE records for discovered types.
					// This prunes a large amount of 'dummy' interfaces out of the API early.
					if (allDiscoveredTypes.Contains(it)) {
						total.Add("TYPEBASE");
						total.Add(typeUdonName);
						total.Add(GetUdonTypeName(it));
					}
				}
			}
		}

		public void Datamine() {
			// -- VRCSDK version --
			string vrcSdkPackageFile = Path.Combine(Application.dataPath, "..", "Packages", "com.vrchat.worlds", "package.json");
			VRCJson.TryDeserializeFromJson(File.ReadAllText(vrcSdkPackageFile), out DataToken vrcSdkPackageToken);
			vrcSdkPackageToken.DataDictionary.TryGetValue("version", out DataToken vrcSdkVersionToken);
			total.Add("VRCSDKVER");
			total.Add(vrcSdkVersionToken.String);
			//
			var nodeDefs = editorInterfaceInstance.GetNodeDefinitions();

			// -- type discovery phase --
			foreach (UdonNodeDefinition und in nodeDefs)
				DiscoverTypes(und);
			foreach (KeyValuePair<string, string> kvp in builtinEventLookup)
				foreach ((string, Type) tuple in builtinEventArgumentsLookup[kvp.Key])
					DiscoverTypes(tuple.Item2);
			// special marker types
			DiscoverTypes(typeof(VRC.Udon.Common.UdonGameObjectComponentHeapReference));
			DiscoverTypes(typeof(VRC.Udon.UdonBehaviour));
			// complete
			AddRecordsType();

			// -- main records --
			foreach (UdonNodeDefinition und in nodeDefs)
				AddRecordExtern(und);

			foreach (KeyValuePair<string, string> kvp in builtinEventLookup) {
				total.Add("EVENT");
				total.Add(kvp.Key);
				total.Add(kvp.Value);
				var args = builtinEventArgumentsLookup[kvp.Key];
				total.Add(args.Length.ToString());
				foreach ((string, Type) tuple in args) {
					total.Add(tuple.Item1);
					DiscoverTypes(tuple.Item2);
					total.Add(GetUdonTypeName(tuple.Item2));
				}
			}
			// add events that aren't exposed
			total.Add("EVENT");
			total.Add("OnAudioFilterRead");
			total.Add("_onAudioFilterRead");
			total.Add("2");
			total.Add("onAudioFilterReadData");
			total.Add("SystemSingleArray");
			total.Add("onAudioFilterReadChannels");
			total.Add("SystemInt32");
			// done!
			total.Add("END");
		}

		// Fun fact: This used to be called 'DatamineZVZ'.
		[MenuItem("VRChat SDK/KDCVRCTools/Datamine")]
		public static void ExtractData() {
			Dataminer dataminer = new();
			dataminer.Datamine();
			System.IO.File.WriteAllText(Application.dataPath + "/datamine.txt", string.Join("\n", dataminer.total));
		}
	}
}
