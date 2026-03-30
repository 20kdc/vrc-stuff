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

		[Tooltip("Maps Quake 2 material names to Unity materials.")]
		[SerializeField]
		public KDCBSPWorkspaceConfig workspace;

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
		public StaticEditorFlags visStaticFlags = StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic | StaticEditorFlags.ContributeGI | StaticEditorFlags.BatchingStatic | StaticEditorFlags.ReflectionProbeStatic;

		/// If set, this overrides the material renderer template.
		/// This is useful if making a case-specific prop using existing materials.
		[Tooltip("If set, this forcibly replaces the prefab used to render materials, even if the workspace specifies its own prefab.\nThis is useful if quickly making a dynamic prop using a static-focused setup.")]
		[SerializeField]
		public LazyLoadReference<GameObject> visOverrideRendererTemplate = null;

		[Tooltip("Controls if/how collision is generated.")]
		[SerializeField]
		public CollisionMode collision = CollisionMode.ConvexBrushes;

		public override void OnImportAsset(AssetImportContext ctx) {

			if (workspace == null) {
				// yes this is modification and Bad but it makes things make more sense really
				workspace = (KDCBSPWorkspaceConfig) AssetDatabase.LoadAssetAtPath("Assets/KDCBSPGameRoot/DefaultWorkspaceConfig.asset", typeof(KDCBSPWorkspaceConfig));
			}

			// setup assignments
			Dictionary<String, KDCBSPWorkspaceConfig.MaterialAssignment> mapping = new();
			foreach (var v in workspace.materials)
				mapping[v.name] = v;

			// this
			UnwrapParam.SetDefaults(out UnwrapParam lightmapSettings);
			lightmapSettings.packMargin = lightmapPackMargin;

			// actually create map meshes
			KDCBSPIntermediate data = KDCBSPIntermediate.Load(File.ReadAllBytes(ctx.assetPath), workspace.worldScale);
			GameObject mapGO = new GameObject("map");

			// we always get this -- we may need it for concave evaluation or for visuals or both
			var triangles = GetBSPTriangles(data, 0);

			if (visuals) {
				GameObject visualsGO = new GameObject("visuals");
				visualsGO.transform.parent = mapGO.transform;

				foreach (var kvp in triangles) {
					var assignment = workspace.fallbackMaterial;

					if (mapping.ContainsKey(kvp.Key))
						assignment = mapping[kvp.Key];

					if (!assignment.material.isSet)
						continue;

					LazyLoadReference<GameObject> rendererTemplate = visOverrideRendererTemplate.isSet ? visOverrideRendererTemplate : assignment.rendererTemplate;

					GameObject materialGO;

					if (rendererTemplate.isSet) {
						materialGO = (GameObject) UnityEngine.Object.Instantiate(rendererTemplate.asset, Vector3.zero, Quaternion.identity, visualsGO.transform);
						materialGO.name = kvp.Key;
					} else {
						materialGO = new GameObject(kvp.Key);
						materialGO.transform.parent = visualsGO.transform;
						GameObjectUtility.SetStaticEditorFlags(materialGO, visStaticFlags);
					}

					Mesh mesh = TrianglesToMesh(kvp.Value, Vector2.one / new Vector2(assignment.width, assignment.height));

					Unwrapping.GenerateSecondaryUVSet(mesh, lightmapSettings);

					ctx.AddObjectToAsset("mesh " + kvp.Key, mesh);

					var meshFilter = materialGO.GetComponent<MeshFilter>();
					if (meshFilter == null)
						meshFilter = materialGO.AddComponent<MeshFilter>();

					var meshRender = materialGO.GetComponent<MeshRenderer>();
					if (meshRender == null)
						meshRender = materialGO.AddComponent<MeshRenderer>();

					var materialsList = new List<Material>();
					materialsList.Add(assignment.material.asset);
					meshRender.SetSharedMaterials(materialsList);

					// mesh.isReadable = false;
					mesh.UploadMeshData(true);
					meshFilter.mesh = mesh;
					if (visOverrideStaticFlags)
						GameObjectUtility.SetStaticEditorFlags(materialGO, visStaticFlags);
				}
			}

			if (collision == CollisionMode.ConvexBrushes) {
				GameObject collisionGO = new GameObject("collision");
				collisionGO.transform.parent = mapGO.transform;

				var convexes = GetBSPConvexes(data, 0);
				var idx = 0;
				foreach (var convex in convexes) {
					string convexName = "convex" + idx;
					GameObject convexGO = new GameObject(convexName);
					convexGO.transform.parent = collisionGO.transform;
					Mesh mesh = TrianglesToMesh(convex, Vector2.one);
					ctx.AddObjectToAsset(convexName, mesh);
					var collider = convexGO.AddComponent(typeof(MeshCollider)) as MeshCollider;
					collider.convex = true;
					collider.sharedMesh = mesh;
					idx++;
				}
			} else if (collision == CollisionMode.ConcaveRoot) {
				List<TriInfo> concave = new();
				foreach (var kvp in triangles) {
					if (kvp.Key == "common/noclip")
						continue;
					foreach (var tri in kvp.Value)
						concave.Add(tri);
				}
				Mesh mesh = TrianglesToMesh(concave, Vector2.one);
				ctx.AddObjectToAsset("concave", mesh);
				var collider = mapGO.AddComponent(typeof(MeshCollider)) as MeshCollider;
				collider.convex = false;
				collider.sharedMesh = mesh;
			}

			ctx.AddObjectToAsset("main obj", mapGO);
			ctx.SetMainObject(mapGO);
		}

		// -- Unity Interface --

		public Mesh TrianglesToMesh(List<TriInfo> triangles, Vector2 uvMul) {
			var vertices = new Vector3[triangles.Count * 3];
			var uvs = new Vector2[triangles.Count * 3];
			var indices = new int[triangles.Count * 3];
			int idx = 0;
			foreach (var v in triangles) {
				vertices[idx] = v.a;
				uvs[idx] = v.au * uvMul;
				indices[idx] = idx;
				idx++;
				vertices[idx] = v.b;
				uvs[idx] = v.bu * uvMul;
				indices[idx] = idx;
				idx++;
				vertices[idx] = v.c;
				uvs[idx] = v.cu * uvMul;
				indices[idx] = idx;
				idx++;
			}
			Mesh res = new Mesh { vertices = vertices, uv = uvs, triangles = indices };
			res.RecalculateNormals();
			res.RecalculateTangents();
			res.Optimize();
			return res;
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

		public List<List<TriInfo>> GetBSPConvexes(KDCBSPIntermediate bsp, int modelIdx) {
			var model = bsp.models[modelIdx];
			List<List<TriInfo>> res = new();
			foreach (var b in model.brushes) {
				// CONTENTS_CURRENT_0
				// We use this as a 'secret handshake' to implement the 'noclip' brush.
				// Noclip brushes are solid (so block vis), but don't create collision.
				if ((b.contents & 0x40000) != 0)
					continue;
				// CONTENTS_SOLID | CONTENTS_PLAYERCLIP
				if ((b.contents & (1 | 0x10000)) == 0)
					continue;
				List<TriInfo> convexMesh = new();
				foreach (var face in bsp.BrushToFaces(b, workspace.worldScale)) {
					bsp.FaceToTriangles(face, convexMesh);
				}
				res.Add(convexMesh);
			}
			return res;
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
