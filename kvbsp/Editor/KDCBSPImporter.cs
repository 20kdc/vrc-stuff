using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.AssetImporters;

using TriInfo = KDCVRCBSP.KDCBSPIntermediate.TriInfo;
using FlagMod = KDCVRCBSP.KDCBSPBrushEntitySettings.FlagMod;
using CollisionMode = KDCVRCBSP.KDCBSPBrushEntitySettings.CollisionMode;

namespace KDCVRCBSP {
	[ScriptedImporter(1, "bsp")]
	public class KDCBSPImporter : ScriptedImporter {

		[Tooltip("Maps Quake 2 material names to Unity materials, among other things.")]
		[SerializeField]
		public LazyLoadReference<KDCBSPAbstractWorkspaceConfig> workspace;

		[Tooltip("Brush entity settings for compiling worldspawn (unless otherwise overridden by parameterizers or properties).")]
		[SerializeField]
		public KDCBSPBrushEntitySettings worldspawnCompilation = new();

		public override void OnImportAsset(AssetImportContext ctx) {
			if (!workspace.isSet) {
				// yes this is modification and Bad but it makes things make more sense really
				workspace = (KDCBSPAbstractWorkspaceConfig) AssetDatabase.LoadAssetAtPath("Assets/KDCBSPGameRoot/DefaultWorkspaceConfig.asset", typeof(KDCBSPWorkspaceConfig));
			}
			var myWorkspace = KDCBSPImportContext.DependsOnArtifact<KDCBSPAbstractWorkspaceConfig>(ctx, workspace);

			KDCBSPIntermediate data = KDCBSPIntermediate.Load(File.ReadAllBytes(ctx.assetPath), myWorkspace.WorldScale);

			List<KDCBSPAbstractWorkspaceConfig> searchOrder = PrepareSearchOrder(ctx, myWorkspace);

			// this
			KDCBSPImportContext importContext = new KDCBSPImportContext {
				importer = this,
				workspace = myWorkspace,
				searchOrder = searchOrder,
				bsp = data,
				assetImportContext = ctx,
				materialCache = new(),
				entityCache = new()
			};

			List<KDCBSPEntityParameterizer> postProcessThese = new();

			GameObject mapGO = CreateEntity(importContext, data.Worldspawn, "worldspawn ", null, postProcessThese);
			if (mapGO == null)
				throw new Exception("worldspawn being gone means something has gone horribly wrong, so rather than risking import corruption we choose to bail here");

			Dictionary<string, int> entCounters = new();
			foreach (var entity in data.entities) {
				if (entity.IsWorldspawn)
					continue;
				// use per-classname counters to increase resilience
				if (!entCounters.ContainsKey(entity.classname))
					entCounters[entity.classname] = 0;
				int eid = entCounters[entity.classname];
				entCounters[entity.classname] = eid + 1;
				// ...
				CreateEntity(importContext, entity, entity.classname + " " + eid, mapGO, postProcessThese);
			}

			// entity tree is complete, postprocess/link
			foreach (var c in postProcessThese) {
				c.EntityPostProcess();
				UnityEngine.Object.DestroyImmediate(c);
			}

			ctx.AddObjectToAsset("main obj", mapGO);
			ctx.SetMainObject(mapGO);
		}

		// -- Primary Entity Converter --

		/// Creates and returns an entity.
		public GameObject CreateEntity(KDCBSPImportContext importContext, KDCBSPIntermediate.Entity entity, string uniqueName, GameObject parent, List<KDCBSPEntityParameterizer> postProcessThese) {
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
			var custom = entGO.GetComponents<KDCBSPEntityParameterizer>();

			var assetPrefix = uniqueName + " ";

			KDCBSPBrushEntitySettings compSettings = (KDCBSPBrushEntitySettings) worldspawnCompilation.Clone();
			foreach (var c in custom) {
				c.EntityParameterize(importContext.bsp, ref entity, uniqueName);
				if (c == null)
					return null;
				compSettings = c.EntityGetBrushSettings(entity.IsWorldspawn, compSettings);
			}

			foreach (var c in custom)
				postProcessThese.Add(c);

			if (compSettings == null || entity.model < 0 || entity.model >= importContext.bsp.models.Length)
				return entGO;

			compSettings.ParseEntityOverrides(entity);

			// Figure out what static flags we want.
			StaticEditorFlags visStaticFlags = GameObjectUtility.GetStaticEditorFlags(entGO);

			void ModSEF(ref StaticEditorFlags sef, FlagMod mod, StaticEditorFlags v) {
				if (mod == FlagMod.On)
					sef |= v;
				else if (mod == FlagMod.Off)
					sef &= ~v;
			}
			ModSEF(ref visStaticFlags, compSettings.contributeGI, StaticEditorFlags.ContributeGI);
			ModSEF(ref visStaticFlags, compSettings.occluderStatic, StaticEditorFlags.OccluderStatic);
			ModSEF(ref visStaticFlags, compSettings.occludeeStatic, StaticEditorFlags.OccludeeStatic);
			ModSEF(ref visStaticFlags, compSettings.batchingStatic, StaticEditorFlags.BatchingStatic);
			ModSEF(ref visStaticFlags, compSettings.reflectionProbeStatic, StaticEditorFlags.ReflectionProbeStatic);

			var model = importContext.bsp.models[entity.model];
			// we always get this -- we may need it for concave evaluation or for visuals or both
			var triangles = GetBSPTriangles(importContext.bsp, model);

			foreach (var kvp in triangles)
				entity.InternalTransformFixup(kvp.Value);

			if (compSettings.visuals) {
				GameObject visualsGO = new GameObject("visuals");

				foreach (var kvp in triangles) {
					var assignment = importContext.LookupMaterial(kvp.Key);
					if (assignment == null)
						continue;

					var materialGO = assignment.BuildVisualObject(importContext, kvp.Key, assetPrefix + "mesh " + kvp.Key, kvp.Value, visualsGO, compSettings);
					if (materialGO == null)
						continue;

					GameObjectUtility.SetStaticEditorFlags(materialGO, visStaticFlags);

					var meshRenderer = materialGO.GetComponent<MeshRenderer>();
					if (meshRenderer != null)
						SetupBrushRenderer(compSettings, assignment, meshRenderer);
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
					var collisionMaterial = bPrimary != null ? bPrimary.collisionMaterial.asset : null;
					// }
					if (!collisionEnable)
						continue;

					// figures out contents and such
					LayerMask layerMask = BrushContentsLayerMaskParameterized(custom, (LayerMask) (1 << entGO.layer), b);

					int layer = KDCBSPUtilities.LayerMaskToLayer(layerMask);
					if (layer == -1)
						continue;

					List<TriInfo> convexMesh = new();

					foreach (var face in importContext.bsp.BrushToFaces(b, importContext.workspace.WorldScale)) {
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
					LayerMask layerMask = BrushContentsLayerMaskParameterized(custom, (LayerMask) (1 << entGO.layer), b);

					if (layerMask == 0)
						continue;

					foreach (var face in importContext.bsp.BrushToFaces(b, importContext.workspace.WorldScale))
						importContext.bsp.FaceToTriangles(face, convexMesh);
				}

				var collisionMaterial = bFullPrimary != null ? bFullPrimary.collisionMaterial.asset : null;

				entity.InternalTransformFixup(convexMesh);

				Mesh mesh = KDCBSPIntermediate.TrianglesToMesh(convexMesh, Vector2.one);
				importContext.assetImportContext.AddObjectToAsset(assetPrefix + "convex", mesh);
				var collider = entGO.AddComponent(typeof(MeshCollider)) as MeshCollider;
				collider.convex = true;
				collider.sharedMaterial = collisionMaterial;
				collider.sharedMesh = mesh;
				compSettings.ApplyColliderSettings(collider);
			}

			return entGO;
		}

		/// Prepares a finished search order.
		public static List<KDCBSPAbstractWorkspaceConfig> PrepareSearchOrder(AssetImportContext ctx, KDCBSPAbstractWorkspaceConfig myWorkspace) {
			List<KDCBSPAbstractWorkspaceConfig> searchOrder = new();
			searchOrder.Add(myWorkspace);
			myWorkspace.BuildSearchOrder(ctx, searchOrder);

			var builtInWorkspace = KDCBSPImportContext.DependsOnArtifact<KDCBSPAbstractWorkspaceConfig>(ctx, KDCBSPUtilities.KVBSP_BASE + "Assets/builtinWorkspace.asset");
			if (builtInWorkspace != null)
				searchOrder.Add(builtInWorkspace);

			return searchOrder;
		}

		/// Prepares a finished search order (for use in non-importer code)
		public static List<KDCBSPAbstractWorkspaceConfig> PrepareSearchOrderEditor(KDCBSPAbstractWorkspaceConfig myWorkspace) {
			List<KDCBSPAbstractWorkspaceConfig> searchOrder = new();
			searchOrder.Add(myWorkspace);
			myWorkspace.BuildSearchOrderEditor(searchOrder);

			var builtInWorkspace = (KDCBSPAbstractWorkspaceConfig) AssetDatabase.LoadAssetAtPath(KDCBSPUtilities.KVBSP_BASE + "Assets/builtinWorkspace.asset", typeof(KDCBSPAbstractWorkspaceConfig));
			if (builtInWorkspace != null)
				searchOrder.Add(builtInWorkspace);

			return searchOrder;
		}

		public static UnwrapParam BrushEntitySettingsToUnwrapParam(KDCBSPBrushEntitySettings compSettings) {
			UnwrapParam.SetDefaults(out UnwrapParam lightmapSettings);
			lightmapSettings.packMargin = compSettings.lightmapPackMargin;
			return lightmapSettings;
		}

		public static void SetupBrushRenderer(KDCBSPBrushEntitySettings compSettings, KDCBSPAbstractMaterialConfig materialDetails, MeshRenderer meshRenderer) {
			if (compSettings.lightmaps == FlagMod.Off)
				meshRenderer.receiveGI = ReceiveGI.LightProbes;
			else if (compSettings.lightmaps == FlagMod.On)
				meshRenderer.receiveGI = ReceiveGI.Lightmaps;
			// lightmap generation can be forced off here
			float res = compSettings.lightmapScale * materialDetails.lightmapScaleMul;
			if (res <= 0.0f) {
				meshRenderer.receiveGI = ReceiveGI.LightProbes;
			} else {
				meshRenderer.scaleInLightmap = res;
			}
		}

		public static LayerMask BrushContentsLayerMaskParameterized(KDCBSPEntityParameterizer[] custom, LayerMask entityLayer, KDCBSPIntermediate.Brush brush) {
			LayerMask layerMask = KDCBSPUtilities.BrushContentsLayerMask(entityLayer, brush.contents);
			foreach (var c in custom)
				layerMask = c.EntityConvexBrushLayer(entityLayer, layerMask, brush);
			return layerMask;
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
