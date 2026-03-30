using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using VRC.SDKBase;

namespace KDCVRCBSP {
	/**
	 * This is a MonoBehaviour put on the root of an entity prefab.
	 */
	public abstract class KDCBSPEntityParameterizer : MonoBehaviour, IEditorOnly {
		/// This is called first.
		/// Note the 'ref' on the entity. This can be used to tweak settings that can't really be easily tweaked from elsewhere, like static flags.
		public virtual void EntityParameterize(KDCBSPIntermediate bsp, ref KDCBSPIntermediate.Entity entity, string uniqueName) {
			// do nothing
		}

		/// Returns the brush entity compile settings for this brush entity.
		public virtual KDCBSPBrushEntitySettings EntityGetBrushSettings(KDCBSPBrushEntitySettings defaultSettings) {
			return defaultSettings;
		}

		public virtual LayerMask EntityConvexBrushLayer(LayerMask myLayer, KDCBSPIntermediate.Brush brush) {
			return EntityConvexBrushLayerWrapper(null, myLayer, brush);
		}

		/// If this is true, then the configured worldspawn static flags are used for this entity's visuals.
		/// Otherwise, it copies the gameobject's static flags, modified by the entity switches.
		public virtual bool EntityUseWorldspawnStaticFlags => false;

		/// Implements default logic if custom == null.
		public static LayerMask EntityConvexBrushLayerWrapper(KDCBSPEntityParameterizer custom, LayerMask myLayer, KDCBSPIntermediate.Brush brush) {
			if (custom != null)
				return custom.EntityConvexBrushLayer(myLayer, brush);
			// CONTENTS_CURRENT_0
			// We use this as a 'secret handshake' to implement the 'noclip' brush.
			// Noclip brushes are solid (so block vis), but don't create collision.
			if ((brush.contents & 0x40000) != 0)
				return 0;
			// CONTENTS_SOLID | CONTENTS_PLAYERCLIP
			if ((brush.contents & (1 | 0x10000)) == 0)
				return 0;
			return myLayer;
		}

		public static int LayerMaskToLayer(LayerMask lm) {
			uint lmi = (uint) (int) lm;
			if (lmi == 0)
				return -1;
			int layer = 0;
			while (lmi != 0) {
				lmi >>= 1;
				layer++;
			}
			return layer;
		}

		/// This is called last.
		public virtual void EntityPostProcess() {
		}
	}
}
