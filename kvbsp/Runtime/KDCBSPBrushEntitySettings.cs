using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using KDCVRCBSP.ECL;

using StaticEditorFlags = KDCVRCBSP.KDCBSPRuntimeStaticEditorFlags;

namespace KDCVRCBSP {
	/**
	 * Brush entity compile settings.
	 * These are all implemented in KDCBSPImporter.
	 */
	[System.Serializable]
	public class KDCBSPBrushEntitySettings : ICloneable {
		/// Handler at KDCBSPUtilities.ImportECLMeshVisual
		[Tooltip("Lightmap pack margin.")]
		[SerializeField]
		public float lightmapPackMargin = 0.01f;

		/// Handler at KDCBSPBrushEntityFlow.SetupBrushRenderer, KDCBSPBrushEntityFlow.Compile
		/// also BuildVisualObject; if LM scale is <= 0.0, we don't unwrap for lightmaps
		[Tooltip("Lightmap scale.")]
		[SerializeField]
		public float lightmapScale = 1f;

		/// Handler at KDCBSPBrushEntityFlow.Compile
		[Tooltip("Enables visuals (as opposed to collision only).")]
		[SerializeField]
		public bool visuals = true;

		/// Handler at KDCBSPAbstractMaterialConfig.Simple.BuildVisualObject
		[Tooltip("If set, this replaces the prefab used to render materials.\nThis cannot override AbstractMaterialConfig subclasses which ignore the flag.\n(If you don't know what this is, you probably don't have to worry about it.)")]
		[SerializeField]
		public LazyLoadReference<GameObject> rendererTemplate = null;

		/// Handler at KDCBSPBrushEntityFlow.Compile
		[Tooltip("Controls if/how collision is generated.")]
		[SerializeField]
		public CollisionMode collision;
		/// Handler at ApplyColliderSettings.
		[Tooltip("Controls the Is Trigger flag on collision.")]
		[SerializeField]
		public bool collisionIsTrigger;

		// UnityEditor-less proxies
		// Handlers at KDCBSPBrushEntityFlow.Compile
		[Tooltip("Overrides the Contribute GI static flag.")]
		[SerializeField]
		public FlagMod contributeGI;
		/// Handler at KDCBSPBrushEntityFlow.SetupBrushRenderer, KDCBSPBrushEntityFlow.Compile
		[Tooltip("Overrides ReceiveGI between lightmaps (on) and light probes (off).")]
		[SerializeField]
		public FlagMod lightmaps;
		[Tooltip("Overrides the Occluder Static static flag.")]
		[SerializeField]
		public FlagMod occluderStatic;
		[Tooltip("Overrides the Occludee Static static flag.")]
		[SerializeField]
		public FlagMod occludeeStatic;
		[Tooltip("Overrides the Batching Static static flag.")]
		[SerializeField]
		public FlagMod batchingStatic;
		[Tooltip("Overrides the Reflection Probe Static static flag.")]
		[SerializeField]
		public FlagMod reflectionProbeStatic;

		[Tooltip("Combined material and enable flag for visleaves to occlusion. This creates editor-only occlusion geometry.")]
		[SerializeField]
		public LazyLoadReference<Material> visleavesToOcclusion = null;

		[Tooltip("Visleaves to occlusion map margin.")]
		[SerializeField]
		public double visleavesToOcclusionMapMargin = 1d;

		[Tooltip("Visleaves to occlusion gap between wall and occlusion.")]
		[SerializeField]
		public double visleavesToOcclusionWallGap = 0.25d;

		public object Clone() {
			return MemberwiseClone();
		}

		/// Handler at KDCBSPEntity.EntityCompile
		public void ParseEntityOverrides(ECLBSPFile.Entity entity) {
			void ParseFlagMod(string s, ref FlagMod mod) {
				string sv = entity[s];
				if (sv == "1")
					mod = FlagMod.On;
				else if (sv == "0")
					mod = FlagMod.Off;
				// -1 is unmodified, and setting it would overwrite any other modification
			}
			lightmapPackMargin = entity.GetFloat("_kdcbsp_lightmap_pack_margin", lightmapPackMargin);
			lightmapScale = entity.GetFloat("_kdcbsp_lightmap_scale", lightmapScale);
			visuals = entity.GetBool("_kdcbsp_visuals", visuals);
			collision = entity.GetEnum<CollisionMode>("_kdcbsp_collision", collision);
			collisionIsTrigger = entity.GetBool("_kdcbsp_collision_trigger", collisionIsTrigger);
			ParseFlagMod("_kdcbsp_contribute_gi", ref contributeGI);
			ParseFlagMod("_kdcbsp_lightmaps", ref lightmaps);
			ParseFlagMod("_kdcbsp_occluder_static", ref occluderStatic);
			ParseFlagMod("_kdcbsp_occludee_static", ref occludeeStatic);
			ParseFlagMod("_kdcbsp_batching_static", ref batchingStatic);
			ParseFlagMod("_kdcbsp_reflection_probe_static", ref reflectionProbeStatic);
		}

		public static void DescribeEntityOverrides(KDCBSPEntityDescriptor descriptor) {
			descriptor.StringKey("_kdcbsp_lightmap_pack_margin", "");
			descriptor.StringKey("_kdcbsp_lightmap_scale", "");
			descriptor.StringKey("_kdcbsp_visuals", "");
			descriptor.StringKey("_kdcbsp_collision", "");
			descriptor.StringKey("_kdcbsp_collision_trigger", "");
			descriptor.StringKey("_kdcbsp_contribute_gi", "");
			descriptor.StringKey("_kdcbsp_lightmaps", "");
			descriptor.StringKey("_kdcbsp_occluder_static", "");
			descriptor.StringKey("_kdcbsp_occludee_static", "");
			descriptor.StringKey("_kdcbsp_batching_static", "");
			descriptor.StringKey("_kdcbsp_reflection_probe_static", "");
		}

		/// Handler at KDCBSPBrushEntityFlow.Compile
		public StaticEditorFlags ModifyStaticEditorFlags(StaticEditorFlags visStaticFlags) {
			void ModSEF(ref StaticEditorFlags sef, FlagMod mod, StaticEditorFlags v) {
				if (mod == FlagMod.On)
					sef |= v;
				else if (mod == FlagMod.Off)
					sef &= ~v;
			}
			ModSEF(ref visStaticFlags, contributeGI, StaticEditorFlags.ContributeGI);
			ModSEF(ref visStaticFlags, occluderStatic, StaticEditorFlags.OccluderStatic);
			ModSEF(ref visStaticFlags, occludeeStatic, StaticEditorFlags.OccludeeStatic);
			ModSEF(ref visStaticFlags, batchingStatic, StaticEditorFlags.BatchingStatic);
			ModSEF(ref visStaticFlags, reflectionProbeStatic, StaticEditorFlags.ReflectionProbeStatic);
			return visStaticFlags;
		}

		/// Applies the settings in this instance to the given collider.
		/// Handler at KDCBSPBrushEntityFlow.Compile
		public void ApplyColliderSettings(Collider collider) {
			collider.isTrigger = collisionIsTrigger;
		}

		/// Flag modifier. Used to proxy staticflags without UnityEditor
		public enum FlagMod {
			FromPrefab,
			Off,
			On
		}

		/// Compose two FlagMods in sequence.
		public static FlagMod FlagModCompose(FlagMod a, FlagMod b) {
			if (b == FlagMod.Off)
				return FlagMod.Off;
			else if (b == FlagMod.On)
				return FlagMod.On;
			return a;
		}

		/// Since TrenchBroom doesn't display enum choice contents inline, we've chosen to recommend string values here.
		public enum CollisionMode {
			/// Collisions are handled using one convex mesh collider per brush.
			/// This generates a lot of Mesh objects, but is great for physics broadphase and produces dependable collisions.
			/// TLDR: Fast and good. If you want collisions, use this whenever possible.
			ConvexBrushes,
			/// Collisions are handled using a concave (triangle-soup) mesh collider attached to the root.
			/// This collider includes faces which the BSP collider included, but which are nodraw. (`common/noclip` is specifically removed by name.)
			/// This collision mode is not recommended for world geometry due to being clip-prone by nature and essentially ruining physics broadphase.
			/// However, it may be of use in props, where interactability is the bigger concern.
			ConcaveRoot,
			/// Single convex mesh made from all brushes on root.
			SingleConvexRoot,
			/// Collisions are not generated.
			None
		}
	}
}
