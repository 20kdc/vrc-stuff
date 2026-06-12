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
	public class KDCBSPEntity : MonoBehaviour, IKDCBSPEntity
		// In VRC, we need this to prevent issues. BasisVR is uncertain.
#if UDON
		, VRC.SDKBase.IEditorOnly
#endif
	{
		public virtual void EntityCompile(IKDCBSPImportContext importContext, ECLBSPFile.Entity entity, string uniqueName) {
			var model = entity.model;
			if (model == null)
				return;

			KDCBSPBrushEntitySettings compSettings = (KDCBSPBrushEntitySettings) importContext.WorldspawnCompilation.Clone();
			bool isWorldspawn = entity == importContext.BSP.worldspawn;
			compSettings = EntityGetBrushSettings(isWorldspawn, compSettings);

			if (compSettings == null)
				return;

			compSettings.ParseEntityOverrides(entity);

			KDCBSPBrushEntityFlow.Compile(gameObject, importContext, isWorldspawn, model, uniqueName, compSettings, (brush) => {
				return EntityConvexBrushLayer(1 << gameObject.layer, brush);
			});
		}

		/// Returns the brush entity compile settings for this brush entity.
		/// The passed instance is a clone of worldspawnCompilation.
		/// If it is worldspawnCompilation, it is already cloned and can thus be freely modified.
		/// If overridden, returned instances must also therefore be safe to modify.
		/// If null is returned, brush entity compilation is disabled. (This can also be achieved by not calling base EntityCompile.)
		public virtual KDCBSPBrushEntitySettings EntityGetBrushSettings(bool isWorldspawn, KDCBSPBrushEntitySettings worldspawnCompilation) {
			return worldspawnCompilation;
		}

		/// Allows picking which layer a brush's convex goes on.
		/// This can also force brushes to be i.e. non-illusionary.
		/// (This might be changed to be per-material in future? Ultimately, the entity will get the final say.)
		public virtual LayerMask EntityConvexBrushLayer(LayerMask entityLayer, ECLBSPFile.Brush brush) {
			return brush.illusionary ? 0 : entityLayer;
		}

		public virtual void EntityPostProcess(IKDCBSPImportContext importContext) {
			UnityEngine.Object.DestroyImmediate(this);
		}
	}
}
