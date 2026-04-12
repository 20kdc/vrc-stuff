using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEditor;
using UnityEditor.AssetImporters;

namespace KDCVRCBSP {
	/**
	 * KDCBSPAbstractMaterialConfig defines things materials care about.
	 */
	public abstract class KDCBSPAbstractMaterialConfig : ScriptableObject {
		/// Not much to this one; if true, it's included in collision by default.
		[Tooltip("Enables/disables collision. This only works on convexes if it wins priority, but it always works on concave root.")]
		[SerializeField]
		public bool collisionEnable = true;

		/// Setup in KDCBSPImporter.SetupBrushRenderer
		[Tooltip("Multiplier for lightmap scale for this material. If zero, this does NOT turn off ContributeGI, just lightmap generation.")]
		[SerializeField]
		public float lightmapScaleMul = 1.0f;

		/// Setup in KDCBSPImporter.CreateEntity
		/// Sets the physics material.
		/// By the way, this has to be changed to PhysicsMaterial when Unity updates. For some reason.
		[Tooltip("Sets the physics material. This only works on convexes if it wins priority, and never works on concave root.")]
		[SerializeField]
		public LazyLoadReference<PhysicMaterial> collisionMaterial;

		/// Builds a material's 'visual'. This is a GameObject, which is returned.
		/// If the importer wishes to override static flags, that's done after BuildVisualObject.
		/// 'data' may be modified as you wish.
		/// A key note: If LightProbeUsage or ReflectionProbeUsage is set to off here, they *stay* off.
		/// This allows the renderer to indicate it doesn't use these features, as an optimization.
		public abstract GameObject BuildVisualObject(KDCBSPImportContext ctx, string materialName, string meshAssetName, List<KDCBSPIntermediate.TriInfo> data, GameObject visualsGO, KDCBSPBrushEntitySettings brushEntitySettings);

		/// Calculates the collision convex priority for a given normal.
		/// This is used when deciding which material config to use for physics materials/etc.
		/// Note that this mechanism is not in use for the concave root mode.
		public abstract float GetCollisionConvexPriority(Vector3 normal);

		/// Contributes ericw-tools and TrenchBroom metadata to the PAK.
		/// Importantly, discoveryPath accounts for the situation where a material config is created on behalf of a material file.
		public abstract void PAKContribute(SortedDictionary<string, byte[]> pakFiles, string materialPath, string discoveryPath);

		public abstract class Simple : KDCBSPAbstractMaterialConfig {
			/// Base priority for determining which material a brush is made of. The normal Y (-1 to 1) is added to this, to bias in favour of floors by default.
			public abstract float BaseCollisionConvexPriority { get; }

			/// Implements retrieving the material information.
			public abstract SimpleMaterialInfo GetMaterial(KDCBSPImportContext ctx, string materialName, string meshAssetName);

			public override GameObject BuildVisualObject(KDCBSPImportContext ctx, string materialName, string meshAssetName, List<KDCBSPIntermediate.TriInfo> data, GameObject visualsGO, KDCBSPBrushEntitySettings brushEntitySettings) {

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

				var uvMul = Vector2.one / mInfo.size;
				// This
				if ((!float.IsFinite(uvMul.x)) || (!float.IsFinite(uvMul.y))) {
					Debug.LogWarning($"Fixing non-finite uvMul in material {materialName} mesh asset {meshAssetName} to prevent lightmapper freeze.\nPlease setup a KDCBSPMaterialConfig with an explicit size!");
					uvMul = Vector2.one;
				}
				Mesh mesh = KDCBSPIntermediate.TrianglesToMesh(data, uvMul);

				Unwrapping.GenerateSecondaryUVSet(mesh, KDCBSPImporter.BrushEntitySettingsToUnwrapParam(brushEntitySettings));

				ctx.assetImportContext.AddObjectToAsset(meshAssetName, mesh);

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

			public override void PAKContribute(SortedDictionary<string, byte[]> pakFiles, string materialPath, string discoveryPath) {
				pakFiles[materialPath] = new byte[0];
			}

			public struct SimpleMaterialInfo {
				public Material material;
				public Vector2 size;
				public bool receiveShadows;
				public UnityEngine.Rendering.ShadowCastingMode shadowCastingMode;
				public bool optForceDisableLightProbes, optForceDisableReflectionProbes;
			}
		}
	}
}
