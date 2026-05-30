using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using KDCVRCBSP.ECL;

namespace KDCVRCBSP {
	/**
	 * The intermediate form used by KDCBSP for import.
	 * If a problem can be simply solved in KDCBSPQ2Loader, it is.
	 * If not, it's solved either here or in KDCBSPImporter.
	 */
	public class KDCBSPIntermediate {
		private Entity worldspawn;

		public Entity Worldspawn => worldspawn;

		/// Parsed entities.
		public List<Entity> entities = new();

		/// Texture information.
		/// In an array in case it needs to be easily cached.
		public TexInfo[] texInfos;

		/// Models. Entities point to these (or implicitly in the case of model 0, aka worldspawn).
		public Model[] models;

		public class Entity: EntityKeys {
			// Auto-parsed early
			public string classname;
			public string targetname;
			public Vector3 origin;
			/// This is how much to translate model positions by to turn them into entity-relative positions.
			/// This accounts for things like auto-origin.
			public Vector3 internalTranslation;
			/// -1 means not a brush model.
			public int model;

			public bool IsWorldspawn => classname == "worldspawn";

			public Entity() {
			}

			public Entity(EntityKeys sourceKeys) : base(sourceKeys) {
			}

			public Vector3 GetVector3Position(string key, Vector3 defaultVal, float worldScale) {
				string[] s3 = this[key].Split(' ');
				if (s3.Length == 3)
					if (float.TryParse(s3[0], out var x))
						if (float.TryParse(s3[1], out var y))
							if (float.TryParse(s3[2], out var z))
								return TransformPosition(x, y, z, worldScale);
				return defaultVal;
			}

			public void FillCore(float worldScale) {
				string detectedClassname = this["classname"];
				if (detectedClassname == "") {
					classname = "info_unknown";
				} else {
					classname = detectedClassname;
				}

				targetname = this["targetname"];

				string detectedModel = this["model"];
				if (detectedModel.StartsWith("*")) {
					if (int.TryParse(detectedModel.Substring(1), out var result)) {
						model = result;
					}
				} else {
					model = IsWorldspawn ? 0 : -1;
				}

				origin = GetVector3Position("origin", Vector3.zero, worldScale);
			}

			/// Transforms a position accounting for internal translation/rotation.
			public Vector3 InternalTransformFixupPos(Vector3 src) {
				return src + internalTranslation;
			}

			/// Transforms a position accounting for internal translation/rotation.
			public void InternalTransformFixup(List<TriInfo> ti) {
				for (int i = 0; i < ti.Count; i++) {
					TriInfo tri = ti[i];
					tri.a = InternalTransformFixupPos(tri.a);
					tri.b = InternalTransformFixupPos(tri.b);
					tri.c = InternalTransformFixupPos(tri.c);
					ti[i] = tri;
				}
			}
		}

		public struct TexInfo {
			public float sX, sY, sZ, sO, tX, tY, tZ, tO;
			public string tex;

			public Vector2 MapUV(Vector3 i) {
				return new Vector2(sO + (i.x * sX) + (i.y * sY) + (i.z * sZ), tO + (i.x * tX) + (i.y * tY) + (i.z * tZ));
			}
		}

		public struct Model {
			public Vector3 mins, maxs, origin;
			public Face[] faces;
			// If explicit UV-mapped face support is required, add 'UVFace' struct and appropriate array here.
			public Brush[] brushes;
		}

		public struct Face {
			public int texInfo;
			public Vector3[] winding;
		}

		public struct Brush {
			public int contents;
			public BrushSide[] sides;
		}

		public struct BrushSide {
			public Plane plane;
			public int texInfo;
		}

		public struct TriInfo {
			public Vector3 a;
			public Vector2 au;
			public Vector3 b;
			public Vector2 bu;
			public Vector3 c;
			public Vector2 cu;
			/// Translates a TriInfo.
			public void Translate(Vector3 by) {
				a += by;
				b += by;
				c += by;
			}
		}

		// -- Loader Picker --

		/// Just a proxy for KDCBSPQ2Loader right now.
		public static KDCBSPIntermediate Load(byte[] bsp, float worldScale) {
			if (bsp.Length < 4)
				throw new Exception("Not even long enough for the magic number!");

			if (bsp[0] == (byte) 'I') {
				return KDCBSPQ2Loader.Load(bsp, worldScale, false);
			} else if (bsp[0] == (byte) 'Q') {
				return KDCBSPQ2Loader.Load(bsp, worldScale, true);
			} else {
				throw new Exception("Doesn't look like a Quake 2 BSP file");
			}
		}

		// -- Loader Assist --

		public void ParseEntities(string entityLump, float worldScale) {
			List<EntityParsed> lumpParsed = MapParser.Parse(entityLump);
			EntityParsed.EnsureWorldspawn(lumpParsed);
			foreach (var entParsed in lumpParsed) {
				Entity entData = new Entity(entParsed.pairs);
				entData.FillCore(worldScale);
				entities.Add(entData);
				if ((worldspawn == null) && entData.IsWorldspawn)
					worldspawn = entData;
			}
		}

		public void SetupBrushEntityOrigins() {
			for (int i = 0; i < entities.Count; i++) {
				var entity = entities[i];
				if (entity["_kdcbsp_autoorigin"] != "1")
					continue;
				if (entity.model < 0 && entity.model > models.Length)
					continue;
				var mdl = models[entity.model];
				var oldOrigin = entity.origin;
				entity.origin = (mdl.mins + mdl.maxs) / 2;
				entity.internalTranslation = oldOrigin - entity.origin;
				entities[i] = entity;
			}
		}

		public void GetEntityBox(Entity entity, out Vector3 centre, out Vector3 size) {
			centre = Vector3.zero;
			size = Vector3.zero;
			if (entity.model < 0 && entity.model > models.Length)
				return;
			var mdl = models[entity.model];
			var bspCentre = (mdl.mins + mdl.maxs) / 2;
			centre = entity.InternalTransformFixupPos(bspCentre);
			size = Vector3.Max(mdl.maxs, mdl.mins) - Vector3.Min(mdl.maxs, mdl.mins);
		}

		// -- High-level Getters --

		/// Gets TexInfo or a fake one.
		/// This is useful because nodraw faces don't necessarily have valid TexInfos.
		/// This comes up in collision processing.
		public TexInfo GetTexInfoOrFallback(int i) {
			if (i >= 0 && i < texInfos.Length)
				return texInfos[i];
			return new TexInfo {
				sX = 1, sY = 0, sZ = 0, sO = 0,
				tX = 0, tY = 1, tZ = 0, tO = 0,
				tex = "fallback"
			};
		}

		// -- Windings --

		/// Transforms triangles into a mesh.
		public static Mesh TrianglesToMesh(List<TriInfo> triangles, Vector2 uvMul) {
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

		/// Adds this face's triangles.
		public void FaceToTriangles(Face f, List<TriInfo> targetList) {
			var uvSrc = GetTexInfoOrFallback(f.texInfo);
			var a = f.winding[0];
			for (int j = 1; j < f.winding.Length - 1; j++) {
				var b = f.winding[j];
				var c = f.winding[j + 1];
				targetList.Add(new TriInfo {
					a = a,
					au = uvSrc.MapUV(a),
					b = b,
					bu = uvSrc.MapUV(b),
					c = c,
					cu = uvSrc.MapUV(c)
				});
			}
		}

		/// Adds this face's triangles, annotated and UV'd with texInfo.
		public void FaceToTriangles(Face f, List<(TriInfo, int)> targetList) {
			var uvSrc = GetTexInfoOrFallback(f.texInfo);
			var a = f.winding[0];
			for (int j = 1; j < f.winding.Length - 1; j++) {
				var b = f.winding[j];
				var c = f.winding[j + 1];
				targetList.Add((new TriInfo {
					a = a,
					au = uvSrc.MapUV(a),
					b = b,
					bu = uvSrc.MapUV(b),
					c = c,
					cu = uvSrc.MapUV(c)
				}, f.texInfo));
			}
		}

		// -- Transform --

		public static Plane TransformPlane(float nX, float nY, float nZ, float d, float worldScale) {
			// [TRANSFORM]
			// So, here's an oddity for you: I don't know why distance has to be inverted.
			// It clearly does, so that's a start, but I don't know why.
			// UPDATE: The reason is because Unity's definition of `distance` is bad.
			// One would think that a point on P exists at (N * D).
			// However, according to the field doc for `distance`, by Unity logic, it is actually at (N * -D).
			return new Plane(new Vector3(nX, nZ, nY), d / -worldScale);
		}

		public static Vector3 TransformPosition(float nX, float nY, float nZ, float worldScale) {
			// [TRANSFORM]
			// positive X in TB is positive X in Unity
			// positive Y in TB is positive Z in Unity
			// positive Z in TB is positive Y in Unity
			return new Vector3(nX, nZ, nY) / worldScale;
		}

		// -- Convex Slicer --

		/// Converts a brush to faces.
		public List<Face> BrushToFaces(Brush brush, float worldScale) {
			Plane3d[] planes = new Plane3d[brush.sides.Length];
			for (int i = 0; i < brush.sides.Length; i++)
				planes[i] = KDCBSPUtilities.ToECL(brush.sides[i].plane);
			List<Face> res = new();
			float epsilon = 0.05f / worldScale;
			float initQuadSize = 131072 / worldScale;
			for (int i = 0; i < brush.sides.Length; i++) {
				// create initial
				List<Vector3d> winding = GeomUtil.GenInitialWinding(planes[i], initQuadSize);
				// cut
				for (int j = 0; j < brush.sides.Length; j++) {
					if (j == i)
						continue;
					planes[j].CutWinding(winding, null, epsilon);
					if (winding.Count < 3)
						break;
				}
				if (winding.Count < 3)
					continue;

				// convert from ECL to Unity
				Vector3[] windingConv = new Vector3[winding.Count];
				for (int j = 0; j < windingConv.Length; j++) {
					// int revIndex = winding.Count - (j + 1);
					windingConv[j] = KDCBSPUtilities.FromECL(winding[j]);
				}

				res.Add(new Face {
					texInfo = brush.sides[i].texInfo,
					winding = windingConv
				});
			}
			return res;
		}
	}
}
