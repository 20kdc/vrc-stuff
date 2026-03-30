using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.AssetImporters;

using TriInfo = KDCVRCBSP.KDCBSPIntermediate.TriInfo;

namespace KDCVRCBSP {
	[ScriptedImporter(1, "bsp")]
	public class KDCBSPImporter : ScriptedImporter {

		[Tooltip("Maps Quake 2 material names to Unity materials, among other things.")]
		[SerializeField]
		public KDCBSPAbstractWorkspaceConfig workspace;

		[Tooltip("Lightmap pack margin.")]
		[SerializeField]
		public float lightmapPackMargin = 0.01f;

		[Tooltip("Enables visuals (as opposed to collision only).")]
		[SerializeField]
		public bool visuals = true;

		/// By default, static flags set in templates are overridden with those here.
		[Tooltip("If set, all renderer static flags are forced to be visStaticFlags.")]
		[SerializeField]
		public bool visOverrideStaticFlags = true;

		[Tooltip("These static flags are used on mesh renderers if there is no template prefab or if visOverrideStaticFlags is set.")]
		[SerializeField]
		public StaticEditorFlags visStaticFlags = StaticEditorFlags.OccludeeStatic | StaticEditorFlags.ContributeGI | StaticEditorFlags.BatchingStatic | StaticEditorFlags.ReflectionProbeStatic;

		[Tooltip("If set, this replaces the prefab used to render materials. This cannot override materials with custom code which ignore the flag.")]
		[SerializeField]
		public LazyLoadReference<GameObject> rendererTemplate = null;

		[Tooltip("Controls if/how collision is generated.")]
		[SerializeField]
		public CollisionMode collision = CollisionMode.ConvexBrushes;

		public override void OnImportAsset(AssetImportContext ctx) {

			if (workspace == null) {
				// yes this is modification and Bad but it makes things make more sense really
				workspace = (KDCBSPAbstractWorkspaceConfig) AssetDatabase.LoadAssetAtPath("Assets/KDCBSPGameRoot/DefaultWorkspaceConfig.asset", typeof(KDCBSPWorkspaceConfig));
			}

			KDCBSPIntermediate data = KDCBSPIntermediate.Load(File.ReadAllBytes(ctx.assetPath), workspace.WorldScale);

			List<KDCBSPAbstractWorkspaceConfig> searchOrder = new();
			searchOrder.Add(workspace);
			workspace.BuildSearchOrder(ctx, searchOrder);

			// this
			UnwrapParam.SetDefaults(out UnwrapParam lightmapSettings);
			lightmapSettings.packMargin = lightmapPackMargin;

			KDCBSPImportContext importContext = new KDCBSPImportContext {
				importer = this,
				lightmapSettings = lightmapSettings,
				workspace = workspace,
				searchOrder = searchOrder,
				bsp = data,
				assetImportContext = ctx,
				materialCache = new()
			};

			// actually create map meshes
			GameObject mapGO = new GameObject("map");

			// we always get this -- we may need it for concave evaluation or for visuals or both
			var triangles = GetBSPTriangles(data, 0);

			if (visuals) {
				GameObject visualsGO = new GameObject("visuals");
				visualsGO.transform.parent = mapGO.transform;

				foreach (var kvp in triangles) {
					var assignment = importContext.LookupMaterial(kvp.Key);
					if (assignment == null)
						continue;

					var materialGO = assignment.BuildVisualObject(importContext, kvp.Key, "mesh " + kvp.Key, kvp.Value, visualsGO);
					if (materialGO == null)
						continue;

					if (visOverrideStaticFlags)
						GameObjectUtility.SetStaticEditorFlags(materialGO, visStaticFlags);
				}
			}

			if (collision == CollisionMode.ConvexBrushes) {
				GameObject collisionGO = new GameObject("collision");
				collisionGO.transform.parent = mapGO.transform;

				var model = data.models[0];
				List<List<TriInfo>> res = new();
				var idx = 0;
				foreach (var b in model.brushes) {
					string convexName = "convex" + idx;
					idx++;
					// figure out primary side {
					KDCBSPAbstractMaterialConfig bPrimary = null;
					float bPrimaryWeight = float.MinValue;
					foreach (var bSide in b.sides) {
						var assignment = importContext.LookupMaterial(data.GetTexInfoOrFallback(bSide.texInfo).tex);
						if (assignment == null)
							continue;
						float weight = assignment.GetCollisionConvexPriority(bSide.plane.normal);
						if (weight > bPrimaryWeight) {
							bPrimary = assignment;
							bPrimaryWeight = weight;
						}
					}
					bool collisionEnable = bPrimary != null ? bPrimary.collisionEnable : true;
					PhysicMaterial collisionMaterial = bPrimary != null ? bPrimary.collisionMaterial.asset : null;
					// }
					if (!collisionEnable)
						continue;
					// CONTENTS_CURRENT_0
					// We use this as a 'secret handshake' to implement the 'noclip' brush.
					// Noclip brushes are solid (so block vis), but don't create collision.
					if ((b.contents & 0x40000) != 0)
						continue;
					// CONTENTS_SOLID | CONTENTS_PLAYERCLIP
					if ((b.contents & (1 | 0x10000)) == 0)
						continue;
					List<TriInfo> convexMesh = new();
					foreach (var face in data.BrushToFaces(b, workspace.WorldScale)) {
						data.FaceToTriangles(face, convexMesh);
					}
					GameObject convexGO = new GameObject(convexName);
					convexGO.transform.parent = collisionGO.transform;
					Mesh mesh = KDCBSPIntermediate.TrianglesToMesh(convexMesh, Vector2.one);
					ctx.AddObjectToAsset(convexName, mesh);
					var collider = convexGO.AddComponent(typeof(MeshCollider)) as MeshCollider;
					collider.convex = true;
					collider.sharedMaterial = collisionMaterial;
					collider.sharedMesh = mesh;
				}
			} else if (collision == CollisionMode.ConcaveRoot) {
				List<TriInfo> concave = new();
				foreach (var kvp in triangles) {
					var assignment = importContext.LookupMaterial(kvp.Key);
					if (assignment != null)
						if (!assignment.collisionEnable)
							continue;
					foreach (var tri in kvp.Value)
						concave.Add(tri);
				}
				Mesh mesh = KDCBSPIntermediate.TrianglesToMesh(concave, Vector2.one);
				ctx.AddObjectToAsset("concave", mesh);
				var collider = mapGO.AddComponent(typeof(MeshCollider)) as MeshCollider;
				collider.convex = false;
				collider.sharedMesh = mesh;
			}

			ctx.AddObjectToAsset("main obj", mapGO);
			ctx.SetMainObject(mapGO);
		}

		// -- Primary Geometry Converters --

		public Dictionary<String, List<TriInfo>> GetBSPTriangles(KDCBSPIntermediate bsp, int modelIdx) {
			var model = bsp.models[modelIdx];
			Dictionary<String, List<TriInfo>> tri = new();
			foreach (var face in model.faces) {
				var winding = face.winding;
				if (winding.Length < 3)
					continue;
				var texinfo = bsp.texInfos[face.texInfo];
				String material = texinfo.tex;
				List<TriInfo> targetList = null;
				if (tri.ContainsKey(material)) {
					targetList = tri[material];
				} else {
					targetList = new();
					tri[material] = targetList;
				}
				bsp.FaceToTriangles(face, targetList);
			}
			return tri;
		}

		// -- Control Enums --

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
			/// Collisions are not generated.
			None
		}
	}
}
