using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using VRC.SDKBase;

namespace KDCVRCBSP {
	/**
	 * This is a MonoBehaviour put on the root of an entity prefab.
	 * Multiple of these may be added. They are applied in GetComponents order.
	 */
	public abstract class KDCBSPEntityParameterizer : MonoBehaviour, IEditorOnly {
		/// This is called first.
		/// Note the 'ref' on the entity. This can be used to tweak settings that can't really be easily tweaked from elsewhere, like static flags.
		/// If you are going to call DestroyImmediate on your GameObject, CALL IT HERE!
		/// This is the only situation where we check; deleting yourself anywhere else will cause chaos.
		/// Notably, deleting your MonoBehaviour here will abort the entity creation process, EVEN IF you don't delete your gameobject or other behaviours.
		/// This can interrupt important things, so don't do that.
		public virtual void EntityParameterize(KDCBSPIntermediate bsp, ref KDCBSPIntermediate.Entity entity, string uniqueName) {
			// do nothing
		}

		/// Returns the brush entity compile settings for this brush entity.
		/// The passed instance is either worldspawnCompilation or the return value from the previous parameterizer.
		/// If it is worldspawnCompilation, it is already cloned and can thus be freely modified.
		/// If overridden, returned instances must also therefore be safe to modify.
		public virtual KDCBSPBrushEntitySettings EntityGetBrushSettings(bool isWorldspawn, KDCBSPBrushEntitySettings prev) {
			return prev;
		}

		/// modLayer defaults to the 'default logic', but is also the output of the last entity parameterizer.
		public virtual LayerMask EntityConvexBrushLayer(LayerMask entityLayer, LayerMask modLayer, KDCBSPIntermediate.Brush brush) {
			return modLayer;
		}

		/// This is called after ALL entities have been built.
		/// You can use this to link targetname fields/etc.
		/// **AFTER THIS IS CALLED, THE MONOBEHAVIOUR IS DESTROYED!!!**
		/// THIS IS YOUR LAST CHANCE TO RUN CODE!!!
		public virtual void EntityPostProcess() {
		}
	}
}
