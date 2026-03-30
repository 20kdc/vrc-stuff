using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEditor;
using UnityEditor.AssetImporters;

namespace KDCVRCBSP {
	/**
	 * KDCBSPMaterialConfig defines things materials care about.
	 */
	[CreateAssetMenu(menuName = "KDCVRCTools/KDCBSP Material Config", fileName = "materialConfig")]
	public class KDCBSPMaterialConfig : KDCBSPAbstractMaterialConfig.Simple {
		[Tooltip("Base priority for determining which material a brush is made of. The normal Y (-1 to 1) is added to this, to bias in favour of floors by default.")]
		[SerializeField]
		public float baseCollisionConvexPriority;

		[Tooltip("Unity material. If not set, this material won't be rendered (but will still exist for colliders).")]
		[SerializeField]
		public LazyLoadReference<Material> material;

		public override float BaseCollisionConvexPriority => baseCollisionConvexPriority;

		[Tooltip("Size of the TrenchBroom 'proxy' material. If zero, attempts to guess.")]
		[SerializeField]
		public Vector2 size = Vector2.zero;

		/// Implements retrieving the material information.
		public override (Material, Vector2) GetMaterial(KDCBSPImportContext ctx, string materialName, string meshAssetName) {
			var m = KDCBSPImportContext.DependsOnArtifact<Material>(ctx.assetImportContext, material);
			Vector2 s = size;
			if (s == Vector2.zero && m != null) {
				Texture tex = m.mainTexture;
				if (tex != null) {
					// scale is deliberately 'ignored' so it works
					s = new Vector2(tex.width, tex.height);
				}
			}
			return (m, s);
		}
	}
}
