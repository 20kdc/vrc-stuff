using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEditor;
using UnityEditor.AssetImporters;
using KDCVRCBSP.ECL;

using FlagMod = KDCVRCBSP.KDCBSPBrushEntitySettings.FlagMod;
using CollisionMode = KDCVRCBSP.KDCBSPBrushEntitySettings.CollisionMode;

namespace KDCVRCBSP {
	public abstract class KDCBSPBaseImporter : ScriptedImporter {

		[Tooltip("Maps material names to Unity materials, among other things.")]
		[SerializeField]
		public LazyLoadReference<KDCBSPAbstractWorkspaceConfig> workspace;

		[Tooltip("Brush entity settings for compiling worldspawn (unless otherwise overridden by parameterizers or properties).")]
		[SerializeField]
		public KDCBSPBrushEntitySettings worldspawnCompilation = new();

		public abstract ECLBSPFile CompileToIntermediate(KDCBSPImportContext importContext, string assetPath);

		public override void OnImportAsset(AssetImportContext ctx) {
			if (!workspace.isSet) {
				// yes this is modification and Bad but it makes things make more sense really
				workspace = (KDCBSPAbstractWorkspaceConfig) AssetDatabase.LoadAssetAtPath("Assets/KDCBSPGameRoot/DefaultWorkspaceConfig.asset", typeof(KDCBSPWorkspaceConfig));
			}
			var myWorkspace = KDCBSPImportContext.DependsOnArtifact<KDCBSPAbstractWorkspaceConfig>(ctx, workspace);

			List<KDCBSPAbstractWorkspaceConfig> searchOrder = myWorkspace.PrepareSearchOrder(ctx);

			// this
			KDCBSPImportContext importContext = new KDCBSPImportContext {
				importer = this,
				workspace = myWorkspace,
				searchOrder = searchOrder,
				bsp = null,
				assetImportContext = ctx,
				materialCache = new(),
				entityCache = new()
			};

			var data = CompileToIntermediate(importContext, assetPath);
			importContext.bsp = data;

			List<KDCBSPEntityParameterizer> postProcessThese = new();

			GameObject mapGO = CreateEntity(importContext, data.worldspawn, "worldspawn", "worldspawn ", null, postProcessThese);
			if (mapGO == null)
				throw new Exception("worldspawn being gone means something has gone horribly wrong, so rather than risking import corruption we choose to bail here");

			Dictionary<string, int> entCounters = new();
			foreach (var entity in data.entities) {
				string classname = entity["classname"];
				if (classname == "")
					classname = "func_unknown";
				if (entity == data.worldspawn)
					continue;
				// use per-classname counters to increase resilience
				if (!entCounters.ContainsKey(classname))
					entCounters[classname] = 0;
				int eid = entCounters[classname];
				entCounters[classname] = eid + 1;
				// ...
				CreateEntity(importContext, entity, classname, classname + " " + eid, mapGO, postProcessThese);
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
		public GameObject CreateEntity(KDCBSPImportContext importContext, ECLBSPFile.Entity entity, string classname, string uniqueName, GameObject parent, List<KDCBSPEntityParameterizer> postProcessThese) {
			var worldScale = importContext.workspace.WorldScale;
			// Create the entity prefab.
			var prefab = importContext.LookupEntity(classname);
			GameObject entGO;
			if (prefab == null) {
				entGO = new GameObject("MissingFallbackPrefab");
				if (parent != null)
					entGO.transform.parent = parent.transform;
			} else if (parent != null) {
				entGO = (GameObject) UnityEngine.Object.Instantiate(prefab, KDCBSPUtilities.TransformPosition(entity.origin, worldScale), Quaternion.identity, parent.transform);
			} else {
				entGO = (GameObject) UnityEngine.Object.Instantiate(prefab);
			}
			// Name it.
			string targetname = entity["targetname"];
			if (targetname != "")
				entGO.name = targetname;
			else
				entGO.name = uniqueName;
			// Find the entity parameterizer.
			var custom = entGO.GetComponents<KDCBSPEntityParameterizer>();

			var assetPrefix = uniqueName + " ";

			KDCBSPBrushEntitySettings compSettings = (KDCBSPBrushEntitySettings) worldspawnCompilation.Clone();
			foreach (var c in custom) {
				c.EntityParameterize(importContext.bsp, entity, uniqueName, worldScale);
				if (c == null)
					return null;
				compSettings = c.EntityGetBrushSettings(entity == importContext.bsp.worldspawn, compSettings);
			}

			foreach (var c in custom)
				postProcessThese.Add(c);

			if (compSettings == null || entity.model == null)
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

			var model = entity.model;

			if (compSettings.visuals) {
				GameObject visualsGO = new GameObject("visuals");

				Dictionary<string, int> texCounters = new();
				foreach (var renderable in model.renderables) {
					var assignment = importContext.LookupMaterial(renderable.tex);
					if (assignment == null)
						continue;

					string nameSuffix = renderable.tex;
					if (texCounters.TryGetValue(renderable.tex, out int counter)) {
						nameSuffix += " " + counter;
						texCounters[renderable.tex] = counter + 1;
					} else {
						texCounters[renderable.tex] = 1;
					}

					List<KDCBSPTriangle> triangles = new();
					foreach (var tri in renderable.Build())
						triangles.Add(KDCBSPTriangle.FromECLTri(tri, worldScale));

					var materialGO = assignment.BuildVisualObject(importContext, nameSuffix, assetPrefix + "mesh " + nameSuffix, triangles, visualsGO, compSettings);
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

			var visleavesToOcclusionMtl = compSettings.visleavesToOcclusion.asset;
			if (visleavesToOcclusionMtl != null && model.viewLeaves.Count > 0) {
				var occlusionGeometry = model.IntoOcclusionGeometry(compSettings.visleavesToOcclusionWallGap * worldScale, compSettings.visleavesToOcclusionMapMargin * worldScale);

				List<KDCBSPTriangle> occlusionMesh = new();

				foreach (var occy in occlusionGeometry)
					KDCBSPTriangle.ConvexToTriangles(occy, occlusionMesh, importContext.workspace.WorldScale);

				GameObject convexGO = new GameObject("occlusion");
				convexGO.transform.parent = entGO.transform;

				Mesh mesh = KDCBSPTriangle.TrianglesToMesh(occlusionMesh, Vector2.one);
				importContext.assetImportContext.AddObjectToAsset(assetPrefix + "occlusion", mesh);

				var meshFilter = convexGO.GetComponent<MeshFilter>();
				if (meshFilter == null)
					meshFilter = convexGO.AddComponent<MeshFilter>();

				var meshRender = convexGO.GetComponent<MeshRenderer>();
				if (meshRender == null)
					meshRender = convexGO.AddComponent<MeshRenderer>();

				var materialsList = new List<Material>();
				materialsList.Add(visleavesToOcclusionMtl);
				meshRender.SetSharedMaterials(materialsList);

				// mesh.isReadable = false;
				mesh.UploadMeshData(true);
				meshFilter.mesh = mesh;

				meshRender.receiveShadows = false;
				meshRender.shadowCastingMode = ShadowCastingMode.Off;
				meshRender.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
				meshRender.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
				convexGO.tag = "EditorOnly";
				GameObjectUtility.SetStaticEditorFlags(convexGO, StaticEditorFlags.OccluderStatic | StaticEditorFlags.BatchingStatic);
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

					List<KDCBSPTriangle> convexMesh = new();

					KDCBSPTriangle.BrushToTriangles(b, convexMesh, importContext.workspace.WorldScale);

					GameObject convexGO = new GameObject(convexName);
					convexGO.transform.parent = collisionGO.transform;
					convexGO.layer = layer;

					Mesh mesh = KDCBSPTriangle.TrianglesToMesh(convexMesh, Vector2.one);
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
				List<KDCBSPTriangle> concave = new();
				foreach (var renderable in model.renderables) {
					var assignment = importContext.LookupMaterial(renderable.tex);
					if (assignment != null)
						if (!assignment.collisionEnable)
							continue;
					foreach (var tri in renderable.Build())
						concave.Add(KDCBSPTriangle.FromECLTri(tri, worldScale));
				}
				Mesh mesh = KDCBSPTriangle.TrianglesToMesh(concave, Vector2.one);
				importContext.assetImportContext.AddObjectToAsset(assetPrefix + "concave", mesh);
				var collider = entGO.AddComponent(typeof(MeshCollider)) as MeshCollider;
				collider.convex = false;
				collider.sharedMesh = mesh;
				compSettings.ApplyColliderSettings(collider);
			} else if (compSettings.collision == CollisionMode.SingleConvexRoot) {
				List<KDCBSPTriangle> convexMesh = new();
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

					KDCBSPTriangle.BrushToTriangles(b, convexMesh, importContext.workspace.WorldScale);
				}

				var collisionMaterial = bFullPrimary != null ? bFullPrimary.collisionMaterial.asset : null;

				Mesh mesh = KDCBSPTriangle.TrianglesToMesh(convexMesh, Vector2.one);
				importContext.assetImportContext.AddObjectToAsset(assetPrefix + "convex", mesh);
				var collider = entGO.AddComponent(typeof(MeshCollider)) as MeshCollider;
				collider.convex = true;
				collider.sharedMaterial = collisionMaterial;
				collider.sharedMesh = mesh;
				compSettings.ApplyColliderSettings(collider);
			}

			return entGO;
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

		public static LayerMask BrushContentsLayerMaskParameterized(KDCBSPEntityParameterizer[] custom, LayerMask entityLayer, ECLBSPFile.Brush brush) {
			LayerMask layerMask = brush.illusionary ? 0 : entityLayer;
			foreach (var c in custom)
				layerMask = c.EntityConvexBrushLayer(entityLayer, layerMask, brush);
			return layerMask;
		}

		public (KDCBSPAbstractMaterialConfig, float) FindPrimarySide(KDCBSPImportContext importContext, ECLBSPFile.Brush brush) {
			KDCBSPAbstractMaterialConfig bPrimary = null;
			float bPrimaryWeight = float.MinValue;
			foreach (var bSide in brush.sides) {
				var assignment = importContext.LookupMaterial(bSide.tex);
				if (assignment == null)
					continue;
				float weight = assignment.GetCollisionConvexPriority(KDCBSPUtilities.TransformNormal(bSide.plane.normal));
				if (weight > bPrimaryWeight) {
					bPrimary = assignment;
					bPrimaryWeight = weight;
				}
			}
			return (bPrimary, bPrimaryWeight);
		}
	}
}
