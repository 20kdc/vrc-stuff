using System.Reflection;
using UnityEngine;
using VRC.Udon;
using VRC.Udon.VM;
using VRC.Udon.Common.Interfaces;

namespace KDCVRCTools {
	/**
	 * Adds options for improved Udon debugging.
	 * Also has some sneaky side effects to help ensure this.
	 * The objective is basically to hook the IUdonVM.
	 */
	[AddComponentMenu("KDCVRCTools/KDC Udon Debug Options")]
	[RequireComponent(typeof(UdonBehaviour))]
	public class KDCUdonDebugOptions : KDCHelperBase {
		private uint _errorPC = 0xFFFFFFFC;
		private UdonVMException _errorExc = null;

		/// Error program counter. We don't want Unity to serialize this.
		public uint ErrorPC {
			get {
				return _errorPC;
			}
			set {
				_errorPC = value;
			}
		}

		public UdonVMException ErrorException {
			get {
				return _errorExc;
			}
			set {
				_errorExc = value;
			}
		}

		[RuntimeInitializeOnLoadMethod]
		private static void SetupErrorHook() {
			KDCUdonHookedVM.OnError -= ErrorHook;
			KDCUdonHookedVM.OnError += ErrorHook;
		}

		public static void ErrorHook(UdonBehaviour ub, UdonVMException vme, uint pc) {
			KDCUdonDebugOptions udo = ub.GetComponent<KDCUdonDebugOptions>();
			udo.ErrorPC = pc;
			udo.ErrorException = vme;
		}
	}
}
