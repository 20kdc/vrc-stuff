using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using KDCVRCBSP.ECL;

using StaticEditorFlags = KDCVRCBSP.KDCBSPRuntimeStaticEditorFlags;
using FlagMod = KDCVRCBSP.KDCBSPBrushEntitySettings.FlagMod;
using CollisionMode = KDCVRCBSP.KDCBSPBrushEntitySettings.CollisionMode;

namespace KDCVRCBSP {
	/// Responsible for all brush entity compilation.
	public static class KDCBSPBrushEntityFlow {
		private const bool Profiling = false;

		public static void Compile(GameObject entGO, IKDCBSPImportContext importContext, bool isWorldspawn, ECLBSPFile.Model model, string assetPrefix, KDCBSPBrushEntitySettings compSettings, Action<Collider, KDCBSPAbstractMaterialConfig, ECLBSPFile.Brush> entColliderSettings) {
			var worldScale = importContext.WorldScale;

			// Figure out what static flags we want.
			StaticEditorFlags visStaticFlags = compSettings.ModifyStaticEditorFlags(KDCBSPUtilities.GetStaticEditorFlags(entGO));

			bool profileThis = Profiling && isWorldspawn;
			System.Diagnostics.Stopwatch stopwatch = profileThis ? new() : null;

			if (stopwatch != null)
				stopwatch.Start();

			GameObject visualsGO = compSettings.visuals ? new GameObject("visuals") : null;
			GameObject collisionGO = (compSettings.collision == CollisionMode.ConvexBrushes) ? new GameObject("collision") : null;
			List<ECLMesh> rootCollision = (compSettings.collision == CollisionMode.ConcaveRoot || compSettings.collision == CollisionMode.SingleConvexRoot) ? new() : null;
			// used for root collision modes, anything renderable goes in here
			KDCBSPAbstractMaterialConfig guessedPrimaryMaterial = null;

			Dictionary<string, int> texCounters = new();

			// Renderables (including bezier patches).
			foreach (var renderable in model.renderables) {
				var assignment = importContext.LookupMaterial(renderable.tex);
				if (assignment == null)
					continue;
				guessedPrimaryMaterial = assignment;

				int collisionLayer = assignment.collisionEnable ? entGO.layer : -1; //KDCBSPUtilities.LayerMaskToLayer(layerMask); FIGURE OUT LAYER REMAPPING
				bool collisionEnable = collisionLayer != -1;

				bool buildRender = visualsGO != null;
				bool buildRootCollision = (rootCollision != null) && collisionEnable;
				bool buildCollisionGO = renderable.concaveCollision && (collisionGO != null) && collisionEnable;

				// If we have nothing to do here, skip this renderable.
				if (!(buildRender || buildCollisionGO || buildRootCollision))
					continue;

				// Ok, now all the expensive stuff, please.

				string nameSuffix = renderable.tex;
				if (texCounters.TryGetValue(renderable.tex, out int counter)) {
					nameSuffix += " " + counter;
					texCounters[renderable.tex] = counter + 1;
				} else {
					texCounters[renderable.tex] = 1;
				}

				ECLMesh eclMesh = renderable.Build();

				// If a default mesh build occurs, we cache it here.
				Mesh[] renderCollReuseCache = new Mesh[1];
				var meshAssetName = assetPrefix + " mesh " + nameSuffix;

				// Build visuals FIRST.
				if (buildRender) {
					var materialGO = assignment.BuildVisualObject(importContext, nameSuffix, meshAssetName + "-custom", eclMesh, (uvMul) => {
						if (renderCollReuseCache[0] != null) {
							Debug.LogWarning($"Rerunning of default build in {meshAssetName}. This is not supported.");
							return renderCollReuseCache[0];
						}

						if ((!float.IsFinite(uvMul.x)) || (!float.IsFinite(uvMul.y))) {
							Debug.LogWarning($"Fixing non-finite uvMul in {meshAssetName} to prevent lightmapper freeze.\nPlease setup a KDCBSPMaterialConfig with an explicit size!");
							uvMul = Vector2.one;
						}

						Mesh res = KDCBSPUtilities.ImportECLMeshVisual(eclMesh, uvMul, worldScale, compSettings);
						importContext.AddObjectToAsset(meshAssetName, res);
						renderCollReuseCache[0] = res;
						return res;
					}, visualsGO, compSettings);

					if (materialGO != null) {
						KDCBSPUtilities.SetStaticEditorFlags(materialGO, visStaticFlags);

						var meshRenderer = materialGO.GetComponent<MeshRenderer>();
						if (meshRenderer != null)
							SetupBrushRenderer(compSettings, assignment, meshRenderer);
					}
				}

				if (buildRootCollision)
					rootCollision.Add(eclMesh);

				if (buildCollisionGO) {
					Mesh mesh = renderCollReuseCache[0];
					if (mesh == null) {
						mesh = KDCBSPUtilities.ImportECLMeshCollision(eclMesh, worldScale);
						importContext.AddObjectToAsset(meshAssetName + "-collider", mesh);
					}

					GameObject concaveGO = new GameObject("concave " + nameSuffix);
					concaveGO.transform.parent = collisionGO.transform;
					concaveGO.layer = collisionLayer;

					var collider = concaveGO.AddComponent(typeof(MeshCollider)) as MeshCollider;
					collider.convex = false;
					collider.sharedMaterial = assignment.collisionMaterial.asset;
					collider.sharedMesh = mesh;
					compSettings.ApplyColliderSettings(collider);
					entColliderSettings(collider, assignment, null);
				}
			}

			if (visualsGO != null) {
				visualsGO.transform.parent = entGO.transform;
				visualsGO.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
			}

			if (collisionGO != null) {
				// In the 'individual convexes' mode, we actually put some effort in.
				var idx = 0;
				foreach (var b in model.brushes) {
					if (b.illusionary)
						continue;

					string convexName = "convex " + idx;
					idx++;
					// figure out primary side {
					(KDCBSPAbstractMaterialConfig bPrimary, float bPrimaryWeight) = FindPrimarySide(importContext, b);
					bool collisionEnable = bPrimary != null ? bPrimary.collisionEnable : true;
					var collisionMaterial = bPrimary != null ? bPrimary.collisionMaterial.asset : null;
					// }
					if (!collisionEnable)
						continue;

					ECLMesh eclMesh = ECLMesh.ToCollisionMesh(b, KDCBSPUtilities.DistanceEpsilon, KDCBSPUtilities.InitialWindingSize);
					Mesh mesh = KDCBSPUtilities.ImportECLMeshCollision(eclMesh, worldScale);
					importContext.AddObjectToAsset(assetPrefix + convexName, mesh);

					GameObject convexGO = new GameObject(convexName);
					convexGO.transform.parent = collisionGO.transform;
					convexGO.layer = entGO.layer;

					var collider = convexGO.AddComponent(typeof(MeshCollider)) as MeshCollider;
					collider.convex = true;
					collider.sharedMaterial = collisionMaterial;
					collider.sharedMesh = mesh;
					compSettings.ApplyColliderSettings(collider);
					entColliderSettings(collider, bPrimary, b);
				}
				collisionGO.transform.parent = entGO.transform;
				collisionGO.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
			}

			if (rootCollision != null) {
				// The root collision built earlier is applied.
				Mesh mesh = KDCBSPUtilities.ImportECLMeshCollision(ECLMesh.Concatenate(rootCollision), worldScale);
				importContext.AddObjectToAsset(assetPrefix + "rootCollision", mesh);
				var collider = entGO.AddComponent(typeof(MeshCollider)) as MeshCollider;
				collider.convex = compSettings.collision == CollisionMode.SingleConvexRoot;
				collider.sharedMaterial = guessedPrimaryMaterial != null ? guessedPrimaryMaterial.collisionMaterial.asset : null;
				collider.sharedMesh = mesh;
				compSettings.ApplyColliderSettings(collider);
				entColliderSettings(collider, guessedPrimaryMaterial, null);
			}

			if (stopwatch != null) {
				stopwatch.Stop();
				Debug.Log($"KVBSP MAIN PROCESSING: {stopwatch.Elapsed}");
			}

			// Occlusion is essentially an entirely separate thing (thankfully).
			var visleavesToOcclusionMtl = compSettings.visleavesToOcclusion.asset;
			if (visleavesToOcclusionMtl != null && model.viewLeaves.Count > 0)
				CompileOcclusionGeometry(importContext, entGO, model, visleavesToOcclusionMtl, compSettings.visleavesToOcclusionWallGap, compSettings.visleavesToOcclusionMapMargin, assetPrefix);
		}

		public static void CompileOcclusionGeometry(IKDCBSPImportContext importContext, GameObject entGO, ECLBSPFile.Model model, Material visleavesToOcclusionMtl, double visleavesToOcclusionWallGap, double visleavesToOcclusionMapMargin, string assetPrefix) {

			if (model.viewLeaves.Count == 0)
				return;

			var worldScale = importContext.WorldScale;

			var occlusionGeometry = ECLOccy.IntoOcclusionGeometry(model, visleavesToOcclusionWallGap * worldScale, visleavesToOcclusionMapMargin * worldScale, KDCBSPUtilities.DistanceEpsilon, KDCBSPUtilities.InitialWindingSize, false);

			List<(Vector3d, IReadOnlyList<Vector3d>)> occlusionMesh = new();

			foreach (var occy in occlusionGeometry)
				foreach (var occyFace in occy.faces)
					occlusionMesh.Add((occyFace.plane.normal, occyFace.winding));

			GameObject convexGO = new GameObject("occlusion");
			convexGO.transform.parent = entGO.transform;

			Mesh mesh = KDCBSPUtilities.ImportECLMeshCollision(ECLMesh.ToCollisionMesh(occlusionMesh), worldScale);
			importContext.AddObjectToAsset(assetPrefix + "occlusion", mesh);

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
			KDCBSPUtilities.SetStaticEditorFlags(convexGO, StaticEditorFlags.OccluderStatic | StaticEditorFlags.BatchingStatic);
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

		public static (KDCBSPAbstractMaterialConfig, float) FindPrimarySide(IKDCBSPImportContext importContext, ECLBSPFile.Brush brush) {
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
