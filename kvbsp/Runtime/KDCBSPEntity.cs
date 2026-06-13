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
		[Tooltip("Details used for the FGD variant of this asset.")]
		[SerializeField]
		public KDCBSPEntityHeader fgdDetails = new();

		public virtual void EntityCompile(IKDCBSPImportContext importContext, ECLBSPFile.Entity entity, string uniqueName) {
			KDCBSPBrushEntityFlow.CompileDefault(gameObject, importContext, entity, uniqueName, EntityGetBrushSettings, EntityBrushApplyColliderSettings);
		}

		/// Returns the brush entity compile settings for this brush entity.
		/// The passed instance is a clone of worldspawnCompilation.
		/// If it is worldspawnCompilation, it is already cloned and can thus be freely modified.
		/// If overridden, returned instances must also therefore be safe to modify.
		/// If null is returned, brush entity compilation is disabled. (This can also be achieved by not calling base EntityCompile.)
		public virtual KDCBSPBrushEntitySettings EntityGetBrushSettings(bool isWorldspawn, KDCBSPBrushEntitySettings worldspawnCompilation) {
			return worldspawnCompilation;
		}

		/// Applies any custom collider settings.
		/// This is applied after any collider settings in the brush entity settings.
		/// This may change layers (the default is to match the entity's layer), and do all sorts of other fun things.
		/// A primary material may not be present under some circumstances.
		/// Note that a brush isn't always present. When it is present, it implies that this collider is solely for that brush.
		/// The illusionary flag has already been processed and accounted for (when possible)
		public virtual void EntityBrushApplyColliderSettings(IKDCBSPImportContext importContext, Collider collider, KDCBSPAbstractMaterialConfig primaryMaterial, ECLBSPFile.Brush brush) {

		}

		public virtual void EntityPostProcess(IKDCBSPImportContext importContext) {
			UnityEngine.Object.DestroyImmediate(this);
		}

		public void EntityFGDAttributes(KDCBSPEntityDescriptor descriptor) {
			fgdDetails.CopyTo(descriptor);
			if (descriptor.isSolid)
				KDCBSPBrushEntitySettings.DescribeEntityOverrides(descriptor);
		}
	}
}
