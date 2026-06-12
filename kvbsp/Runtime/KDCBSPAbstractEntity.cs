using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using KDCVRCBSP.ECL;

namespace KDCVRCBSP {
	/**
	 * This is a MonoBehaviour put on the root of an entity prefab.
	 * They define everything about the entity prefab's compilation (apart from initial instantiation).
	 * There may only be one of these.
	 * These should be editor-only.
	 */
	public abstract class KDCBSPAbstractEntity : MonoBehaviour
		// In VRC, we need this to prevent issues. BasisVR is uncertain.
#if UDON
		, VRC.SDKBase.IEditorOnly
#endif
	{
		/// Entity compile.
		public abstract void EntityCompile(IKDCBSPImportContext importContext, ECLBSPFile.Entity entity, string uniqueName);

		/// This is called after ALL entities have been built.
		/// You can use this to link targetname fields/etc.
		/// **AFTER THIS IS CALLED, THE MONOBEHAVIOUR IS DESTROYED!!!**
		/// THIS IS YOUR LAST CHANCE TO RUN CODE!!!
		public abstract void EntityPostProcess(IKDCBSPImportContext importContext);
	}
}
