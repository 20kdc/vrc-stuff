using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using KDCVRCBSP.ECL;

namespace KDCVRCBSP {
	/**
	 * See KDCBSPAbstractEntity.
	 * This implementation sets up a 'standard' brush entity flow.
	 */
	public class KDCBSPEntity : KDCBSPAbstractEntity {
		public override void EntityCompile(IKDCBSPImportContext importContext, ECLBSPFile.Entity entity, string uniqueName) {
			KDCBSPBrushEntityFlow.Compile(gameObject, this, importContext, entity, uniqueName);
		}

		/// Returns the brush entity compile settings for this brush entity.
		/// The passed instance is either worldspawnCompilation or the return value from the previous parameterizer.
		/// If it is worldspawnCompilation, it is already cloned and can thus be freely modified.
		/// If overridden, returned instances must also therefore be safe to modify.
		public virtual KDCBSPBrushEntitySettings EntityGetBrushSettings(bool isWorldspawn, KDCBSPBrushEntitySettings prev) {
			return prev;
		}

		/// modLayer defaults to the 'default logic', but is also the output of the last entity parameterizer.
		public virtual LayerMask EntityConvexBrushLayer(LayerMask entityLayer, LayerMask modLayer, ECLBSPFile.Brush brush) {
			return modLayer;
		}

		public override void EntityPostProcess(IKDCBSPImportContext importContext) {
		}
	}
}
