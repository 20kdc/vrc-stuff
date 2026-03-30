using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.AssetImporters;

using TriInfo = KDCVRCBSP.KDCBSPIntermediate.TriInfo;
using FlagMod = KDCVRCBSP.KDCBSPIntermediate.FlagMod;
using CollisionMode = KDCVRCBSP.KDCBSPBrushEntitySettings.CollisionMode;

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

		[Tooltip("These static flags are set on worldspawn.")]
		[SerializeField]
		public StaticEditorFlags worldspawnStaticFlags = StaticEditorFlags.OccludeeStatic | StaticEditorFlags.ContributeGI | StaticEditorFlags.BatchingStatic | StaticEditorFlags.ReflectionProbeStatic;

		[Tooltip("If set, this replaces the prefab used to render materials. This cannot override materials with custom code which ignore the flag.")]
		[SerializeField]
		public LazyLoadReference<GameObject> rendererTemplate = null;

		public KDCBSPBrushEntitySettings brushCompilation = new();

		public override void OnImportAsset(AssetImportContext ctx) {
			if (workspace == null) {
				// yes this is modification and Bad but it makes things make more sense really
				workspace = (KDCBSPAbstractWorkspaceConfig) AssetDatabase.LoadAssetAtPath("Assets/KDCBSPGameRoot/DefaultWorkspaceConfig.asset", typeof(KDCBSPWorkspaceConfig));
			}

			KDCBSPIntermediate data = KDCBSPIntermediate.Load(File.ReadAllBytes(ctx.assetPath), workspace.WorldScale);

			List<KDCBSPAbstractWorkspaceConfig> searchOrder = new();
			searchOrder.Add(workspace);
			workspace.BuildSearchOrder(ctx, searchOrder);

			var builtInWorkspace = KDCBSPImportContext.DependsOnArtifact<KDCBSPAbstractWorkspaceConfig>(ctx, KDCBSPImportContext.KVBSP_BASE + "Assets/builtinWorkspace.asset");
			if (builtInWorkspace != null)
				searchOrder.Add(builtInWorkspace);

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
				materialCache = new(),
				entityCache = new()
			};

			GameObject mapGO = CreateEntity(importContext, data.Worldspawn, "worldspawn ", null);

			int eid = 0;

			foreach (var entity in data.entities) {
				if (entity.IsWorldspawn)
					continue;
				CreateEntity(importContext, entity, entity.classname + " " + (eid++), mapGO);
			}

			ctx.AddObjectToAsset("main obj", mapGO);
			ctx.SetMainObject(mapGO);
		}

		// -- Primary Entity Converter --

		/// Creates and returns an entity.
		public GameObject CreateEntity(KDCBSPImportContext importContext, KDCBSPIntermediate.Entity entity, string uniqueName, GameObject parent) {
			// Create the entity prefab.
			var prefab = importContext.LookupEntity(entity.classname);
			GameObject entGO;
			if (prefab == null) {
				entGO = new GameObject("MissingFallbackPrefab");
				if (parent != null)
					entGO.transform.parent = parent.transform;
			} else if (parent != null) {
				entGO = (GameObject) UnityEngine.Object.Instantiate(prefab, entity.origin, Quaternion.identity, parent.transform);
			} else {
				entGO = (GameObject) UnityEngine.Object.Instantiate(prefab);
			}
			// Name it.
			if (entity.targetname != "")
				entGO.name = entity.targetname;
			else
				entGO.name = uniqueName;
			// Find the entity parameterizer.
			var custom = entGO.GetComponent<KDCBSPEntityParameterizer>();
			var compSettings = brushCompilation;

			var assetPrefix = uniqueName + " ";

			if (custom != null) {
				custom.EntityParameterize(importContext.bsp, ref entity, uniqueName);
				compSettings = custom.EntityGetBrushSettings(compSettings);
			}

			if (compSettings == null || entity.model < 0 || entity.model >= importContext.bsp.models.Length) {
				if (custom != null)
					custom.EntityPostProcess();
				return entGO;
			}

			// Figure out what static flags we want.
			StaticEditorFlags visStaticFlags = GameObjectUtility.GetStaticEditorFlags(entGO);
			if ((custom != null && custom.EntityUseWorldspawnStaticFlags) || entity.IsWorldspawn)
				visStaticFlags = worldspawnStaticFlags;

			void ModSEF(ref StaticEditorFlags sef, FlagMod mod, StaticEditorFlags v) {
				if (mod == FlagMod.On)
					sef |= v;
				else if (mod == FlagMod.Off)
					sef &= ~v;
			}
			ModSEF(ref visStaticFlags, entity.contributeGI, StaticEditorFlags.ContributeGI);
			ModSEF(ref visStaticFlags, entity.occluderStatic, StaticEditorFlags.OccluderStatic);
			ModSEF(ref visStaticFlags, entity.occludeeStatic, StaticEditorFlags.OccludeeStatic);
			ModSEF(ref visStaticFlags, entity.batchingStatic, StaticEditorFlags.BatchingStatic);
			ModSEF(ref visStaticFlags, entity.reflectionProbeStatic, StaticEditorFlags.ReflectionProbeStatic);

			var model = importContext.bsp.models[entity.model];
			// we always get this -- we may need it for concave evaluation or for visuals or both
			var triangles = GetBSPTriangles(importContext.bsp, model);

			foreach (var kvp in triangles)
				entity.InternalTransformFixup(kvp.Value);

			if (visuals) {
				GameObject visualsGO = new GameObject("visuals");

				foreach (var kvp in triangles) {
					var assignment = importContext.LookupMaterial(kvp.Key);
					if (assignment == null)
						continue;

					var materialGO = assignment.BuildVisualObject(importContext, kvp.Key, assetPrefix + "mesh " + kvp.Key, kvp.Value, visualsGO);
					if (materialGO == null)
						continue;

					GameObjectUtility.SetStaticEditorFlags(materialGO, visStaticFlags);
				}

				visualsGO.transform.parent = entGO.transform;
				visualsGO.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
			}

			if (compSettings.collision == CollisionMode.ConvexBrushes) {
				GameObject collisionGO = new GameObject("collision");

				var idx = 0;
				foreach (var b in model.brushes) {
					string convexName = "convex" + idx;
					idx++;
					// figure out primary side {
					(KDCBSPAbstractMaterialConfig bPrimary, float bPrimaryWeight) = FindPrimarySide(importContext, b);
					bool collisionEnable = bPrimary != null ? bPrimary.collisionEnable : true;
					PhysicMaterial collisionMaterial = bPrimary != null ? bPrimary.collisionMaterial.asset : null;
					// }
					if (!collisionEnable)
						continue;

					// figures out contents and such
					LayerMask layerMask = KDCBSPEntityParameterizer.EntityConvexBrushLayerWrapper(custom, (LayerMask) (1 << entGO.layer), b);
					int layer = KDCBSPEntityParameterizer.LayerMaskToLayer(layerMask);
					if (layer == -1)
						continue;

					List<TriInfo> convexMesh = new();

					foreach (var face in importContext.bsp.BrushToFaces(b, workspace.WorldScale)) {
						importContext.bsp.FaceToTriangles(face, convexMesh);
					}

					entity.InternalTransformFixup(convexMesh);

					GameObject convexGO = new GameObject(convexName);
					convexGO.transform.parent = collisionGO.transform;
					convexGO.layer = layer;

					Mesh mesh = KDCBSPIntermediate.TrianglesToMesh(convexMesh, Vector2.one);
					importContext.assetImportContext.AddObjectToAsset(assetPrefix + convexName, mesh);
					var collider = convexGO.AddComponent(typeof(MeshCollider)) as MeshCollider;
					collider.convex = true;
					collider.sharedMaterial = collisionMaterial;
					collider.sharedMesh = mesh;
					compSettings.ApplyColliderSettings(collider);
				}
				collisionGO.transform.parent = entGO.transform;
				collisionGO.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
			} else if (compSettings.collision == CollisionMode.ConcaveRoot) {
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
				importContext.assetImportContext.AddObjectToAsset(assetPrefix + "concave", mesh);
				var collider = entGO.AddComponent(typeof(MeshCollider)) as MeshCollider;
				collider.convex = false;
				collider.sharedMesh = mesh;
				compSettings.ApplyColliderSettings(collider);
			} else if (compSettings.collision == CollisionMode.SingleConvexRoot) {
				List<TriInfo> convexMesh = new();
				var idx = 0;
				// Note the attempt to find a 'true' primary material.
				// The hope is that this works decently well, but no promises.
				KDCBSPAbstractMaterialConfig bFullPrimary = null;
				float bFullPrimaryWeight = float.MinValue;
				foreach (var b in model.brushes) {
					string convexName = "convex" + idx;
					idx++;
					// figure out primary side {
					(KDCBSPAbstractMaterialConfig bPrimary, float bPrimaryWeight) = FindPrimarySide(importContext, b);
					bool collisionEnable = bPrimary != null ? bPrimary.collisionEnable : true;
					// }

					if (!collisionEnable)
						continue;

					if (bPrimaryWeight > bFullPrimaryWeight) {
						bFullPrimary = bPrimary;
						bFullPrimaryWeight = bPrimaryWeight;
					}

					// figures out contents and such
					LayerMask layerMask = KDCBSPEntityParameterizer.EntityConvexBrushLayerWrapper(custom, (LayerMask) (1 << entGO.layer), b);
					if (layerMask == 0)
						continue;

					foreach (var face in importContext.bsp.BrushToFaces(b, workspace.WorldScale))
						importContext.bsp.FaceToTriangles(face, convexMesh);
				}

				PhysicMaterial collisionMaterial = bFullPrimary != null ? bFullPrimary.collisionMaterial.asset : null;

				entity.InternalTransformFixup(convexMesh);

				Mesh mesh = KDCBSPIntermediate.TrianglesToMesh(convexMesh, Vector2.one);
				importContext.assetImportContext.AddObjectToAsset(assetPrefix + "convex", mesh);
				var collider = entGO.AddComponent(typeof(MeshCollider)) as MeshCollider;
				collider.convex = true;
				collider.sharedMaterial = collisionMaterial;
				collider.sharedMesh = mesh;
				compSettings.ApplyColliderSettings(collider);
			}

			if (custom != null)
				custom.EntityPostProcess();

			return entGO;
		}

		public (KDCBSPAbstractMaterialConfig, float) FindPrimarySide(KDCBSPImportContext importContext, KDCBSPIntermediate.Brush brush) {
			KDCBSPAbstractMaterialConfig bPrimary = null;
			float bPrimaryWeight = float.MinValue;
			foreach (var bSide in brush.sides) {
				var assignment = importContext.LookupMaterial(importContext.bsp.GetTexInfoOrFallback(bSide.texInfo).tex);
				if (assignment == null)
					continue;
				float weight = assignment.GetCollisionConvexPriority(bSide.plane.normal);
				if (weight > bPrimaryWeight) {
					bPrimary = assignment;
					bPrimaryWeight = weight;
				}
			}
			return (bPrimary, bPrimaryWeight);
		}

		// -- Primary Geometry Converters --

		public Dictionary<String, List<TriInfo>> GetBSPTriangles(KDCBSPIntermediate bsp, KDCBSPIntermediate.Model model) {
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
	}
}
