using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using KDCVRCBSP.ECL;

namespace KDCVRCBSP {
	/**
	 * Performs a lot of the upper 'management' tasks necessary for any simple material.
	 */
	public abstract class KDCBSPSimpleAbstractMaterialConfig : KDCBSPAbstractMaterialConfig {
		/// Base priority for determining which material a brush is made of. The normal Y (-1 to 1) is added to this, to bias in favour of floors by default.
		public abstract float BaseCollisionConvexPriority { get; }

		/// Implements retrieving the material information.
		public abstract SimpleMaterialInfo GetMaterial(IKDCBSPImportContext ctx, string materialName, string meshAssetName);

		public override GameObject BuildVisualObject(IKDCBSPImportContext ctx, string materialName, string meshAssetName, ECLMesh data, Func<Vector2, Mesh> buildDefaultMesh, GameObject visualsGO, KDCBSPBrushEntitySettings brushEntitySettings) {

			var mInfo = GetMaterial(ctx, materialName, meshAssetName);

			if (mInfo.material == null)
				return null;

			GameObject materialGO;

			if (brushEntitySettings.rendererTemplate.isSet) {
				materialGO = (GameObject) UnityEngine.Object.Instantiate(brushEntitySettings.rendererTemplate.asset, Vector3.zero, Quaternion.identity, visualsGO.transform);
				materialGO.name = materialName;
			} else {
				materialGO = new GameObject(materialName);
				materialGO.transform.parent = visualsGO.transform;
			}

			// Q3 uses premultiplied UV, while other BSP formats require we convert here.
			var uvMul = ctx.BSP.uvPremultiplied ? Vector2.one : Vector2.one / mInfo.size;
			Mesh mesh = buildDefaultMesh(uvMul);

			var meshFilter = materialGO.GetComponent<MeshFilter>();
			if (meshFilter == null)
				meshFilter = materialGO.AddComponent<MeshFilter>();

			var meshRender = materialGO.GetComponent<MeshRenderer>();
			if (meshRender == null)
				meshRender = materialGO.AddComponent<MeshRenderer>();

			var materialsList = new List<Material>();
			materialsList.Add(mInfo.material);
			meshRender.SetSharedMaterials(materialsList);

			// mesh.isReadable = false;
			mesh.UploadMeshData(true);
			meshFilter.mesh = mesh;

			meshRender.receiveShadows = mInfo.receiveShadows;
			meshRender.shadowCastingMode = mInfo.shadowCastingMode;
			if (mInfo.optForceDisableLightProbes)
				meshRender.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
			if (mInfo.optForceDisableReflectionProbes)
				meshRender.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;

			return materialGO;
		}

		public override float GetCollisionConvexPriority(Vector3 normal) {
			return BaseCollisionConvexPriority + normal.y;
		}

		public virtual SimpleIconInfo PAKGetTrenchBroomTextureSimple(string materialPath, string discoveryPath) {
			return new SimpleIconInfo {
				source = null,
				offset = Vector2.zero,
				scale = Vector2.zero,
				iconSize = Vector2.zero
			};
		}

		public override Texture2D PAKGetTrenchBroomTexture(string materialPath, string discoveryPath) {
			var ovr = PAKGetTrenchBroomTextureOverride(discoveryPath);
			if (ovr != null)
				return ovr;

			var iconInfo = PAKGetTrenchBroomTextureSimple(materialPath, discoveryPath);
			if (iconInfo.source == null)
				return base.PAKGetTrenchBroomTexture(materialPath, discoveryPath);
			int width = (int) iconInfo.iconSize.x;
			if (width < 1)
				width = 1;
			int height = (int) iconInfo.iconSize.y;
			if (height < 1)
				height = 1;
			var rt = new RenderTexture(width, height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
			Graphics.Blit(iconInfo.source, rt, iconInfo.scale, iconInfo.offset);
			return KDCBSPUtilities.ReadRenderTexture(rt);
		}

		public struct SimpleMaterialInfo {
			public Material material;
			public Vector2 size;
			public bool receiveShadows;
			public UnityEngine.Rendering.ShadowCastingMode shadowCastingMode;
			public bool optForceDisableLightProbes, optForceDisableReflectionProbes;
		}

		/// Icon information.
		/// This represents a possible MainTex.
		/// If source is null, this is invalid.
		public struct SimpleIconInfo {
			public Texture source;
			public Vector2 offset;
			public Vector2 scale;
			/// Should match SimpleMaterialInfo.size
			public Vector2 iconSize;
		}
	}
}
