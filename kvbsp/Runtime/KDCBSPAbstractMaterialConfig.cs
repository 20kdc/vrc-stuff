using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using KDCVRCBSP.ECL;

// BasisVR compat
// This is Unity being extremely dumb
#if UNITY_2
using PhysicsMaterial = UnityEngine.PhysicMaterial;
#elif UNITY_3
using PhysicsMaterial = UnityEngine.PhysicMaterial;
#elif UNITY_4
using PhysicsMaterial = UnityEngine.PhysicMaterial;
#elif UNITY_5
using PhysicsMaterial = UnityEngine.PhysicMaterial;
#elif UNITY_2017
using PhysicsMaterial = UnityEngine.PhysicMaterial;
#elif UNITY_2018
using PhysicsMaterial = UnityEngine.PhysicMaterial;
#elif UNITY_2019
using PhysicsMaterial = UnityEngine.PhysicMaterial;
#elif UNITY_2020
using PhysicsMaterial = UnityEngine.PhysicMaterial;
#elif UNITY_2020
using PhysicsMaterial = UnityEngine.PhysicMaterial;
#elif UNITY_2021
using PhysicsMaterial = UnityEngine.PhysicMaterial;
#elif UNITY_2022
using PhysicsMaterial = UnityEngine.PhysicMaterial;
#elif UNITY_2023
using PhysicsMaterial = UnityEngine.PhysicMaterial;
#endif

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
		[Tooltip("Sets the physics material. This only works on convexes if it wins priority, and never works on concave root.")]
		[SerializeField]
		public LazyLoadReference<PhysicsMaterial> collisionMaterial;

		/// Builds a material's 'visual'. This is a GameObject, which is returned.
		/// If the importer wishes to override static flags, that's done after BuildVisualObject.
		/// A key note: If LightProbeUsage or ReflectionProbeUsage is set to off here, they *stay* off.
		/// This allows the renderer to indicate it doesn't use these features, as an optimization.
		/// 'buildDefaultMesh' takes a UV multiplier and returns a Unity mesh. This mesh is setup to be reusable for collision if applicable.
		public abstract GameObject BuildVisualObject(IKDCBSPImportContext ctx, string materialName, string meshAssetName, ECLMesh data, Func<Vector2, Mesh> buildDefaultMesh, GameObject visualsGO, KDCBSPBrushEntitySettings brushEntitySettings);

		/// Calculates the collision convex priority for a given normal.
		/// This is used when deciding which material config to use for physics materials/etc.
		/// Note that this mechanism is not in use for the concave root mode.
		public abstract float GetCollisionConvexPriority(Vector3 normal);

		/// Contributes ericw-tools and TrenchBroom metadata to the PAK.
		/// Importantly, discoveryPath accounts for the situation where a material config is created on behalf of a material file.
		/// Note that because this logic is 'pretty universal', even for really 'unusual' materials, it's implemented here.
		public virtual void PAKContribute(SortedDictionary<string, byte[]> pakFiles, string materialPath, string discoveryPath) {
			var tex = PAKGetTrenchBroomTexture(materialPath, discoveryPath);
			if (tex == null) {
				Debug.LogWarning("NO ICON: " + PAKGetTrenchBroomTextureOverridePath(discoveryPath));
				tex = KDCBSPUtilities.ReadLPImageOrNull(KDCBSPUtilities.KVBSP_BASE + "Editor/Icons/noIcon.png");
			}
			pakFiles["textures/" + materialPath + ".png"] = tex.EncodeToPNG();
			var walJSON = PAKGetWALJSON(materialPath, discoveryPath);
			if (walJSON != null)
				pakFiles["textures/" + materialPath + ".wal_json"] = walJSON;
		}

		public string PAKGetTrenchBroomTextureOverridePath(string discoveryPath) {
			return Path.Join(Path.GetDirectoryName(discoveryPath), Path.GetFileNameWithoutExtension(discoveryPath) + ".png");
		}

		/// Gets the 'override' icon, if any.
		/// You SHOULD use this in PAKGetTrenchBroomTexture.
		public Texture2D PAKGetTrenchBroomTextureOverride(string discoveryPath) {
			string hypothesis = PAKGetTrenchBroomTextureOverridePath(discoveryPath);
			return KDCBSPUtilities.ReadLPImageOrNull(hypothesis);
		}

		/// Gets the texture as shown in TrenchBroom. Here, one Quake unit == 1 pixel.
		/// Note that this can return null. In that event, a default is loaded and used.
		public virtual Texture2D PAKGetTrenchBroomTexture(string materialPath, string discoveryPath) {
			return PAKGetTrenchBroomTextureOverride(discoveryPath);
		}

		/// Gets the .wal_json file contents.
		/// If null, no such file is made.
		public virtual byte[] PAKGetWALJSON(string materialPath, string discoveryPath) {
			string hypothesis = Path.Join(Path.GetDirectoryName(discoveryPath), Path.GetFileNameWithoutExtension(discoveryPath) + ".wal_json");
			return KDCBSPUtilities.ReadLPBytesOrNull(hypothesis);
		}

		public virtual string PAKGetQ3Shader(string materialPath, string discoveryPath) {
			if (materialPath.Contains(" "))
				return "";
			// if possible, we need this for netradiant-custom to work
			return "{\nmap textures/" + materialPath + ".png\nrgbGen identity\n}";
		}
	}
}
