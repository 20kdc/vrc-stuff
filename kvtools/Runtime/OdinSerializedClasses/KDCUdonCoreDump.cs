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
	}
}
