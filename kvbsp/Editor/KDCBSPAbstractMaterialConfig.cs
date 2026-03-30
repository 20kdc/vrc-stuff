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

		/// Sets the physics material.
		/// By the way, this has to be changed to PhysicsMaterial when Unity updates. For some reason.
		[Tooltip("Sets the physics material. This only works on convexes if it wins priority, and never works on concave root.")]
		[SerializeField]
		public LazyLoadReference<PhysicMaterial> collisionMaterial;

		/// Builds a material's 'visual'. This is a GameObject, which is returned.
		/// If the importer wishes to override static flags, that's done after BuildVisualObject.
		/// 'data' may be modified as you wish.
		public abstract GameObject BuildVisualObject(KDCBSPImportContext ctx, string materialName, string meshAssetName, List<KDCBSPIntermediate.TriInfo> data, GameObject visualsGO, KDCBSPBrushEntitySettings brushEntitySettings);

		/// Calculates the collision convex priority for a given normal.
		/// This is used when deciding which material config to use for physics materials/etc.
		/// Note that this mechanism is not in use for the concave root mode.
		public abstract float GetCollisionConvexPriority(Vector3 normal);

		public abstract class Simple : KDCBSPAbstractMaterialConfig {
			/// Base priority for determining which material a brush is made of. The normal Y (-1 to 1) is added to this, to bias in favour of floors by default.
			public abstract float BaseCollisionConvexPriority { get; }

			/// Implements retrieving the material information.
			public abstract (Material, Vector2) GetMaterial(KDCBSPImportContext ctx, string materialName, string meshAssetName);

			public override GameObject BuildVisualObject(KDCBSPImportContext ctx, string materialName, string meshAssetName, List<KDCBSPIntermediate.TriInfo> data, GameObject visualsGO, KDCBSPBrushEntitySettings brushEntitySettings) {

				var (material, size) = GetMaterial(ctx, materialName, meshAssetName);

				if (material == null)
					return null;

				GameObject materialGO;

				if (brushEntitySettings.rendererTemplate.isSet) {
					materialGO = (GameObject) UnityEngine.Object.Instantiate(brushEntitySettings.rendererTemplate.asset, Vector3.zero, Quaternion.identity, visualsGO.transform);
					materialGO.name = materialName;
				} else {
					materialGO = new GameObject(materialName);
					materialGO.transform.parent = visualsGO.transform;
				}

				Mesh mesh = KDCBSPIntermediate.TrianglesToMesh(data, Vector2.one / size);

				Unwrapping.GenerateSecondaryUVSet(mesh, KDCBSPImporter.BrushEntitySettingsToUnwrapParam(brushEntitySettings));

				ctx.assetImportContext.AddObjectToAsset(meshAssetName, mesh);

				var meshFilter = materialGO.GetComponent<MeshFilter>();
				if (meshFilter == null)
					meshFilter = materialGO.AddComponent<MeshFilter>();

				var meshRender = materialGO.GetComponent<MeshRenderer>();
				if (meshRender == null)
					meshRender = materialGO.AddComponent<MeshRenderer>();

				var materialsList = new List<Material>();
				materialsList.Add(material);
				meshRender.SetSharedMaterials(materialsList);

				// mesh.isReadable = false;
				mesh.UploadMeshData(true);
				meshFilter.mesh = mesh;
				return materialGO;
			}

			public override float GetCollisionConvexPriority(Vector3 normal) {
				return BaseCollisionConvexPriority + normal.y;
			}
		}
	}
}
