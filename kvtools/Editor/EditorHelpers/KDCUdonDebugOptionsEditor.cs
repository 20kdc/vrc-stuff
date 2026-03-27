using System.Reflection;
using System.IO;
using UnityEngine;
using UnityEditor;
using VRC.Udon;
using VRC.Udon.Common.Interfaces;
using VRC.Udon.Serialization.OdinSerializer;
using VRC.Udon.Serialization.OdinSerializer.Utilities;

namespace KDCVRCTools {
	/**
	 * Adds options for improved Udon debugging.
	 */
	[CustomEditor(typeof(KDCUdonDebugOptions))]
	public class KDCUdonDebugOptionsEditor : UnityEditor.Editor {
		private bool showException = false;

		public override void OnInspectorGUI() {
			KDCUdonDebugOptions targetUDO = (KDCUdonDebugOptions) target;
			UdonBehaviour ub = targetUDO.gameObject.GetComponent<UdonBehaviour>();
			if (ub == null)
				return;
			IUdonVM vm = KDCUdonHookedVM.GetUdonVM(ub);
			if (vm != null) {
				var (stackItems, stackSize) = KDCUdonHookedVM.GetUdonStack(vm);
				if (targetUDO.ErrorPC != 0xFFFFFFFC) {
					EditorGUILayout.LabelField("Error:");
					var ex = targetUDO.ErrorException;
					EditorGUI.indentLevel++;
					if (ex != null) {
						showException = EditorGUILayout.Foldout(showException, "Exception");
						if (showException)
							EditorGUILayout.TextArea(ex.ToString());
					}
					EditorGUILayout.IntField("PC", (int) targetUDO.ErrorPC);
					EditorGUILayout.TextField("...", UdonRationalizePC(vm, targetUDO.ErrorPC));
					if (GUILayout.Button("Dump Error To File")) {
						uint[] cleanStack = null;
						if (stackItems != null) {
							cleanStack = new uint[stackSize];
							for (int i = 0; i < stackSize; i++)
								cleanStack[i] = stackItems[i];
						}
						DumpProcess("Udon Error Dump...", new KDCUdonCoreDump(vm.RetrieveProgram(), targetUDO.ErrorPC, vm.InspectHeap(), cleanStack));
					}
					EditorGUI.indentLevel--;
				} else if (ub.HasError) {
					EditorGUILayout.LabelField("Unhooked error! Heap dump is available, but no program counter or exception info.");
				} else {
					EditorGUILayout.LabelField("OK");
				}
				// stack
				if (stackItems != null) {
					EditorGUILayout.LabelField("Stack:");
					EditorGUI.indentLevel++;
					for (var i = 0; i < stackSize; i++) {
						EditorGUILayout.IntField("Error PC", (int) stackItems[i]);
					}
					EditorGUI.indentLevel--;
				}
				// dump heap
				if (GUILayout.Button("Dump Heap"))
					DumpProcess("Udon Heap Dump...", vm.InspectHeap());
			} else {
				GUILayout.Label("UdonBehaviour has no VM; not in play mode?", EditorStyles.wordWrappedLabel);
			}
		}

		private static void DumpProcess(string title, object obj) {
			string filename = EditorUtility.SaveFilePanel(title, "", "", "bin");
			if (filename != "") {
				byte[] serializedBin = VRC.Udon.Serialization.OdinSerializer.SerializationUtility.SerializeValue(obj, DataFormat.Binary, out var serializedUEOBin);
				File.WriteAllBytes(filename, serializedBin);
			}
		}

		private static string UdonFormatPC(uint pc) {
			return "(0x" + System.Convert.ToString(pc, 16).PadLeft(8, '0') + ")";
		}

		private static string UdonRationalizePC(IUdonVM vm, uint pc) {
			if (pc > 0x80000000U)
				return "ABORT " + UdonFormatPC(pc);
			IUdonProgram program = vm.RetrieveProgram();
			string sym = "UNKNOWN";
			uint v = 0;
			foreach (string xsym in program.EntryPoints.GetSymbols()) {
				uint addr = program.EntryPoints.GetAddressFromSymbol(xsym);
				if (addr == pc) {
					return xsym + " " + UdonFormatPC(pc);
				} else if (addr <= pc && (v == 0 || addr > v)) {
					sym = xsym + "+";
					v = addr;
				}
			}
			return sym + " " + UdonFormatPC(pc);
		}
	}
}
