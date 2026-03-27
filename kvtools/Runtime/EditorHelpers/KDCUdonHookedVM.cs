using System;
using System.Reflection;
using UnityEngine;
using VRC.Udon;
using VRC.Udon.VM;
using VRC.Udon.VM.Common;
using VRC.Udon.Common.Interfaces;

namespace KDCVRCTools {
	/**
	 * This is a wrapper around an IUdonVM.
	 * Its job is to provide error reports.
	 */
	public class KDCUdonHookedVM : IUdonVM, IUdonVMHook {
		/// Reports errors from all hooked Udon behaviours
		public static Action<UdonBehaviour, UdonVMException, uint> OnError { get; set; } = null;
		public static bool PleaseHookEverything = false;

		public readonly UdonBehaviour parent;
		public readonly IUdonVM wrapped;

		public IUdonVM Wrapped => wrapped;

		public KDCUdonHookedVM(UdonBehaviour parent, IUdonVM wrapped) {
			this.parent = parent;
			this.wrapped = wrapped;
		}

		public bool LoadProgram(IUdonProgram program) {
			return wrapped.LoadProgram(program);
		}

		public IUdonProgram RetrieveProgram() {
			return wrapped.RetrieveProgram();
		}

		public void SetProgramCounter(uint value) {
			wrapped.SetProgramCounter(value);
		}

		public uint GetProgramCounter() {
			return wrapped.GetProgramCounter();
		}

		/// This is what we came here to hook.
		public uint Interpret() {
			try {
				return wrapped.Interpret();
			} catch (UdonVMException vme) {
				if (OnError != null) {
					try {
						OnError(parent, vme, wrapped.GetProgramCounter());
					} catch (Exception ex) {
						Debug.LogException(ex);
					}
				}
				throw vme;
			}
		}

		public IUdonHeap InspectHeap() {
			return wrapped.InspectHeap();
		}

		public bool DebugLogging {
			get {
				return wrapped.DebugLogging;
			}
			set {
				wrapped.DebugLogging = value;
			}
		}

		[RuntimeInitializeOnLoadMethod]
		private static void SetupHookOpportunity() {
			UdonBehaviour.OnInit += HookOpportunity;
		}

		/// We don't want to hook a behaviour more than once.
		/// If someone else is hooking behaviours, log a warning.
		public static void HookOpportunity(UdonBehaviour ub, IUdonProgram program) {
			// to prevent unwanted issues, we don't hook any VMs that don't have our special component.
			// at some point this might be changed to an attribute
			if ((!PleaseHookEverything) && !ub.GetComponent<KDCUdonDebugOptions>()) {
				return;
			}
			IUdonVM vm = GetUdonVM(ub);
			UdonVM vmx = vm as UdonVM;
			if (vmx == null) {
				Debug.LogWarning("KDCUdonHookedVM: Someone else (" + vm.GetType() + ") is hooking an Udon VM we were intending to hook. Comment out this warning and TELL ME if you know, for sure, it's safe. If you know it's unsafe, put a return statement in this block.");
			}
			// install the hook
			SetUdonVM(ub, new KDCUdonHookedVM(ub, vm));
		}

		public static IUdonVM GetUdonVM(UdonBehaviour ub) {
			return (IUdonVM) typeof(UdonBehaviour).GetField("_udonVM", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(ub);
		}

		public static void SetUdonVM(UdonBehaviour ub, IUdonVM vm) {
			typeof(UdonBehaviour).GetField("_udonVM", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(ub, vm);
		}

		public static UdonVM GetDirectUdonVM(IUdonVM vm) {
			while (true) {
				IUdonVMHook kvm = vm as IUdonVMHook;
				if (kvm != null) {
					vm = kvm.Wrapped;
					continue;
				}

				UdonVM tvm = vm as UdonVM;
				if (tvm != null)
					return tvm;

				return null;
			}
		}

		public static (uint[], int) GetUdonStack(IUdonVM vm) {
			UdonVM dvm = GetDirectUdonVM(vm);
			if (dvm == null)
				return (null, 0);
			return GetUdonStackDirect(dvm);
		}

		public static (uint[], int) GetUdonStackDirect(UdonVM vm) {
			object stack = typeof(UdonVM).GetField("_stack", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(vm);
			var stackType = stack.GetType();
			uint[] arr = (uint[]) stackType.GetField("_array", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(stack);
			int sz = (int) stackType.GetField("_size", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(stack);
			return (arr, sz);
		}
	}
}
