using System;
using System.Reflection;
using UnityEngine;
using VRC.Udon;
using VRC.Udon.VM;
using VRC.Udon.Common.Interfaces;

namespace KDCVRCTools {
	/**
	 * This is a wrapper around an IUdonVM.
	 */
	public interface IUdonVMHook : IUdonVM {
		IUdonVM Wrapped { get; }
	}
}
