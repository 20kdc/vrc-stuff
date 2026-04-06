using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;

namespace KDCVRCBSP {
	/**
	 * The intermediate form used by KDCBSP for import.
	 * If a problem can be simply solved in KDCBSPQ2Loader, it is.
	 * If not, it's solved either here or in KDCBSPImporter.
	 */
	public class KDCBSPIntermediate {
		/// Parsed entities.
		public List<Entity> entities = new();

		/// Texture information.
		/// In an array in case it needs to be easily cached.
		public TexInfo[] texInfos;

		/// Models. Entities point to these (or implicitly in the case of model 0, aka worldspawn).
		public Model[] models;

		public struct Entity {
			public List<(string, string)> pairs;
			// Auto-parsed early
			public string classname;
			public string targetname;
			public Vector3 origin;
			// This is how much to translate BSP positions by.
			// This accounts for things like auto-origin.
			public Vector3 internalTranslation;
			// -1 means not a brush model.
			public int model;

			public bool IsWorldspawn => classname == "worldspawn";

			public string this[string key] {
				get {
					foreach (var (tkey, tvalue) in pairs)
						if (key == tkey)
							return tvalue;
					return "";
				}
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

			public bool GetBool(string key, bool defaultVal) {
				if (this[key] == "1")
					return true;
				if (this[key] == "0")
					return false;
				return defaultVal;
			}

			public int GetInt(string key, int defaultVal) {
				if (int.TryParse(this[key], out var val))
					return val;
				return defaultVal;
			}

			public E GetEnum<E>(string key, E defaultVal) where E : struct {
				if (Enum.TryParse<E>(this[key], out E res))
					return res;
				return defaultVal;
			}

			public float GetFloat(string key, float defaultVal) {
				if (float.TryParse(this[key], out var val))
					return val;
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

		public List<(string, bool)> TokenizeEntities(string entityLump) {
			char[] whitespace = new char[33];
			for (int i = 0; i < 33; i++)
				whitespace[i] = (char) i;
			List<(string, bool)> tokens = new();
			while (entityLump.Length >= 0) {
				entityLump = entityLump.TrimStart(whitespace);
				if (entityLump.Length == 0)
					break;
				if (entityLump.StartsWith('"')) {
					int endPoint = entityLump.IndexOf('\"', 1);
					string token = entityLump.Substring(1, endPoint - 1);
					tokens.Add((token, true));
					entityLump = entityLump.Substring(endPoint + 1);
				} else {
					int endPoint = entityLump.IndexOfAny(whitespace);
					if (endPoint == -1) {
						tokens.Add((entityLump, false));
						break;
					} else {
						tokens.Add((entityLump.Substring(0, endPoint), false));
						entityLump = entityLump.Substring(endPoint);
					}
				}
			}
			return tokens;
		}

		public void ParseEntities(string entityLump, float worldScale) {
			List<(string, bool)> tokens = TokenizeEntities(entityLump);
			// We try to be extremely permissive.
			List<(string, string)> currentEntity = new();
			string key = null;
			foreach (var (text, quoted) in tokens) {
				if (quoted) {
					if (key != null) {
						currentEntity.Add((key, text));
						key = null;
					} else {
						key = text;
					}
				} else if (text == "{") {
					key = null;
					currentEntity = new();
				} else if (text == "}") {
					Entity entData = new Entity {
						pairs = currentEntity
					};
					entData.FillCore(worldScale);
					entities.Add(entData);
				}
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

		// -- High-level Getters --

		public Entity Worldspawn {
			get {
				foreach (Entity e in entities)
					if (e.IsWorldspawn)
						return e;
				// Synthetic worldspawn
				List<(string, string)> keys = new();
				keys.Add(("classname", "worldspawn"));
				Entity synthetic = new Entity {
					pairs = keys
				};
				// since it's synthetic, we can use a fake worldScale here
				synthetic.FillCore(64.0f);
				return synthetic;
			}
		}

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
			List<Plane> planes = new();
			foreach (var side in brush.sides)
				planes.Add(side.plane);
			List<Face> res = new();
			float epsilon = 0.05f / worldScale;
			float initQuadSize = 131072 / worldScale;
			for (int i = 0; i < brush.sides.Length; i++) {
				// create initial
				List<Vector3> winding = GenInitialWinding(brush.sides[i].plane, initQuadSize);
				// cut
				for (int j = 0; j < brush.sides.Length; j++) {
					if (j == i)
						continue;
					winding = CutWinding(winding, brush.sides[j].plane, epsilon);
				}
				if (winding.Count == 0)
					continue;
				res.Add(new Face {
					texInfo = brush.sides[i].texInfo,
					winding = winding.ToArray()
				});
			}
			return res;
		}

		// Creates an initial winding for a given plane.
		public static List<Vector3> GenInitialWinding(Plane p, float q) {
			// This algorithm in particular is subject to numerical stability issues.
			// If you're getting precision issues with collision: LOOK HERE FIRST!
			// float q = 1024;
			List<Vector3> res = new();
			var nx = Math.Abs(p.normal.x);
			var ny = Math.Abs(p.normal.y);
			var nz = Math.Abs(p.normal.z);
			if (nx > ny && nx > nz) {
				// X-major
				res.Add(p.ClosestPointOnPlane(new Vector3( 0,  q, -q)));
				res.Add(p.ClosestPointOnPlane(new Vector3( 0,  q,  q)));
				res.Add(p.ClosestPointOnPlane(new Vector3( 0, -q,  q)));
				res.Add(p.ClosestPointOnPlane(new Vector3( 0, -q, -q)));
			} else if (ny > nz) {
				// Y-major
				res.Add(p.ClosestPointOnPlane(new Vector3( q,  0, -q)));
				res.Add(p.ClosestPointOnPlane(new Vector3( q,  0,  q)));
				res.Add(p.ClosestPointOnPlane(new Vector3(-q,  0,  q)));
				res.Add(p.ClosestPointOnPlane(new Vector3(-q,  0, -q)));
			} else {
				// Z-major
				res.Add(p.ClosestPointOnPlane(new Vector3( q, -q,  0)));
				res.Add(p.ClosestPointOnPlane(new Vector3( q,  q,  0)));
				res.Add(p.ClosestPointOnPlane(new Vector3(-q,  q,  0)));
				res.Add(p.ClosestPointOnPlane(new Vector3(-q, -q,  0)));
			}
			// hacky way to do this without much more work
			Plane p2 = new Plane(res[0], res[1], res[2]);
			if (Vector3.Dot(p.normal, p2.normal) < 0.5f)
				res.Reverse();
			p2 = new Plane(res[0], res[1], res[2]);
			if ((Vector3.Dot(p.normal, p2.normal) < 0.9f) || (Math.Abs(p.distance - p2.distance) > 0.1f))
				throw new Exception("GenInitialWinding created bad winding for " + p + ", resulted in " + p2);
			return res;
		}

		// Cuts a winding. Everything on the positive edge of the plane is lost.
		// Winding order is maintained.
		public static List<Vector3> CutWinding(List<Vector3> lst, Plane p, float epsilon) {
			List<Vector3> res = new();
			for (int i = 0; i < lst.Count; i++) {
				int j = (i + 1) % lst.Count;
				// We handle the winding as a series of edges.
				// We're considered 'responsible' for the first point.
				var pi = lst[i];
				var si = SideOfPoint(p, pi, epsilon);
				var pj = lst[j];
				var sj = SideOfPoint(p, pj, epsilon);
				// Determine the intersection type.
				if (si <= 0) {
					// I is negative or on plane, so will always be preserved.
					res.Add(pi);
				}
				if ((si < 0 && sj > 0) || (si > 0 && sj < 0)) {
					// Line crosses through plane.
					// One of these points will be deleted entirely. The line that crosses back through the plane will generate its own intersection point.
					// 'Raycast' didn't work(?), so don't use it.
					// Let's say that the line is headed positive. distI = -2, distJ = 1.
					// Therefore, travel = 3, and the desired lerp value is 0.6666r.
					float distI = p.GetDistanceToPoint(pi);
					float distJ = p.GetDistanceToPoint(pj);
					float travel = distJ - distI;
					// Well, it's this.
					float lerpPtr = (-distI) / travel;
					res.Add(Vector3.LerpUnclamped(pi, pj, lerpPtr));
				}
			}
			return res;
		}

		public static int SideOfPoint(Plane p, Vector3 point, float epsilon) {
			float dist = p.GetDistanceToPoint(point);
			if (Math.Abs(dist) < epsilon)
				return 0;
			if (dist < 0)
				return -1;
			return 1;
		}
	}
}
