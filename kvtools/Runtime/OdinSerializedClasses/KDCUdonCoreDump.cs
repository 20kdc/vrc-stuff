using System;
using System.Reflection;
using UnityEngine;
using VRC.Udon;
using VRC.Udon.VM;
using VRC.Udon.VM.Common;
using VRC.Udon.Common.Interfaces;

namespace KDCVRCTools {
	/**
	 * This is a very special class.
	 * It's intended to be serialized using OdinSerializer.
	 */
	[Serializable]
	public class KDCUdonCoreDump {
		public IUdonProgram program;
		public uint errorPC;
		public IUdonHeap heap;
		public uint[] stack;

		public KDCUdonCoreDump(IUdonProgram program, uint errorPC, IUdonHeap heap, uint[] stack) {
			this.program = program;
			this.errorPC = errorPC;
			this.heap = heap;
			this.stack = stack;
		}

		public static KDCUdonCoreDump CoreDump(IUdonVM vm, uint pc) {
			var (stackItems, stackSize) = KDCUdonHookedVM.GetUdonStack(vm);
			uint[] cleanStack = null;
			if (stackItems != null) {
				cleanStack = new uint[stackSize];
				for (int i = 0; i < stackSize; i++)
					cleanStack[i] = stackItems[i];
			}
			return new KDCUdonCoreDump(vm.RetrieveProgram(), pc, vm.InspectHeap(), cleanStack);
		}
	}
}
