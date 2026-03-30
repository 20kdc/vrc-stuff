using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.AssetImporters;

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
			List<(string, string)> pairs;
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
			public int headNode;
			public Face[] faces;
			// If explicit UV-mapped face support is required, add 'UVFace' struct and appropriate array here.
			/// For now, Q2Loader is copying the same brushes array to all models.
			/// This is subject to being fixed.
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
			return KDCBSPQ2Loader.Load(bsp, worldScale);
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
