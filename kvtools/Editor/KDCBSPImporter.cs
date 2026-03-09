using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.AssetImporters;

namespace KDCVRCTools {
	[ScriptedImporter(1, "bsp")]
	public class KDCBSPImporter : ScriptedImporter {

		// [TRANSFORM]
		// This is calibrated to vr_player_stick
		[SerializeField]
		public float worldScale = 64.0f;

		public override void OnImportAsset(AssetImportContext ctx) {
			byte[] data = File.ReadAllBytes(ctx.assetPath);
			GameObject mapGO = new GameObject("map");
			var triangles = GetBSPTriangles(data);
			foreach (var kvp in triangles) {
				GameObject materialGO = new GameObject(kvp.Key);
				materialGO.transform.parent = mapGO.transform;
				Mesh mesh = TrianglesToMesh(kvp.Value);
				ctx.AddObjectToAsset("mesh " + kvp.Key, mesh);
				var meshFilter = materialGO.AddComponent(typeof(MeshFilter)) as MeshFilter;
				materialGO.AddComponent(typeof(MeshRenderer));
				// mesh.isReadable = false;
				mesh.UploadMeshData(true);
				meshFilter.mesh = mesh;
				var staticFlags = StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic | StaticEditorFlags.ContributeGI | StaticEditorFlags.BatchingStatic | StaticEditorFlags.ReflectionProbeStatic;
				GameObjectUtility.SetStaticEditorFlags(materialGO, staticFlags);
			}
			var convexes = GetBSPConvexes(data);
			var idx = 0;
			foreach (var convex in convexes) {
				string convexName = "convex" + idx;
				Mesh mesh = TrianglesToMesh(convex);
				ctx.AddObjectToAsset(convexName, mesh);
				var collider = mapGO.AddComponent(typeof(MeshCollider)) as MeshCollider;
				collider.name = convexName;
				collider.convex = true;
				collider.sharedMesh = mesh;
				idx++;
			}
			ctx.AddObjectToAsset("main obj", mapGO);
			ctx.SetMainObject(mapGO);
		}

		// -- Unity Interface --

		public Mesh TrianglesToMesh(List<TriInfo> triangles) {
			var vertices = new Vector3[triangles.Count * 3];
			var uvs = new Vector2[triangles.Count * 3];
			var indices = new int[triangles.Count * 3];
			int idx = 0;
			foreach (var v in triangles) {
				vertices[idx] = v.a;
				uvs[idx] = v.au;
				indices[idx] = idx;
				idx++;
				vertices[idx] = v.b;
				uvs[idx] = v.bu;
				indices[idx] = idx;
				idx++;
				vertices[idx] = v.c;
				uvs[idx] = v.cu;
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

		public Dictionary<String, List<TriInfo>> GetBSPTriangles(byte[] bsp) {
			int modelOfs = GetBSPStructOfs(bsp, 13, 0, 48);
			Dictionary<String, List<TriInfo>> tri = new();
			int firstFace = BitConverter.ToInt32(bsp, modelOfs + 40);
			int numFaces = BitConverter.ToInt32(bsp, modelOfs + 44);
			for (int i = 0; i < numFaces; i++) {
				var winding = GetBSPFaceWinding(bsp, firstFace + i);
				if (winding.Count == 0)
					continue;
				String material = "test";
				List<TriInfo> targetList = null;
				if (tri.ContainsKey(material)) {
					targetList = tri[material];
				} else {
					targetList = new();
					tri[material] = targetList;
				}
				var a = winding[0];
				for (int j = 1; j < winding.Count - 1; j++) {
					var b = winding[j];
					var c = winding[j + 1];
					targetList.Add(new TriInfo {
						a = a,
						au = Vector2.zero,
						b = b,
						bu = Vector2.zero,
						c = c,
						cu = Vector2.zero
					});
				}
			}
			return tri;
		}

		public List<List<TriInfo>> GetBSPConvexes(byte[] bsp) {
			int numBrushes = GetBSPStructCount(bsp, 14, 12);
			List<List<TriInfo>> res = new();
			for (int i = 0; i < numBrushes; i++) {
				int brushOfs = GetBSPStructOfs(bsp, 14, i, 12);
				int firstSide = BitConverter.ToInt32(bsp, brushOfs);
				int numSides = BitConverter.ToInt32(bsp, brushOfs + 4);
				List<Plane> planes = new();
				for (int j = 0; j < numSides; j++) {
					planes.Add(GetBSPBrushSide(bsp, firstSide + j));
				}
				res.Add(CutConvex(planes));
			}
			return res;
		}

		// -- Convex Slicer --

		// Creates a convex from planes.
		public static List<TriInfo> CutConvex(List<Plane> planes) {
			List<TriInfo> res = new();
			for (int i = 0; i < planes.Count; i++) {
				// create initial
				List<Vector3> winding = GenInitialWinding(planes[i]);
				// cut
				for (int j = 0; j < planes.Count; j++) {
					if (j == i)
						continue;
					winding = CutWinding(winding, planes[j]);
				}
				if (winding.Count == 0)
					continue;
				// submit as triangles
				var a = winding[0];
				for (int j = 1; j < winding.Count - 1; j++) {
					var b = winding[j];
					var c = winding[j + 1];
					res.Add(new TriInfo {
						a = a,
						au = Vector2.zero,
						b = b,
						bu = Vector2.zero,
						c = c,
						cu = Vector2.zero
					});
				}
			}
			return res;
		}

		// Creates an initial winding for a given plane.
		public static List<Vector3> GenInitialWinding(Plane p) {
			float q = 131072;
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
		public static List<Vector3> CutWinding(List<Vector3> lst, Plane p) {
			List<Vector3> res = new();
			for (int i = 0; i < lst.Count; i++) {
				int j = (i + 1) % lst.Count;
				// We handle the winding as a series of edges.
				// We're considered 'responsible' for the first point.
				var pi = lst[i];
				var si = SideOfPoint(p, pi);
				var pj = lst[j];
				var sj = SideOfPoint(p, pj);
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

		public static int SideOfPoint(Plane p, Vector3 point) {
			float epsilon = 0.05f;
			float dist = p.GetDistanceToPoint(point);
			if (Math.Abs(dist) < epsilon)
				return 0;
			if (dist < 0)
				return -1;
			return 1;
		}

		// -- BSP access core --

		public static int GetBSPStructOfs(byte[] bsp, int lump, int index, int structLen) {
			int offset = BitConverter.ToInt32(bsp, 8 + (lump * 8));
			int length = BitConverter.ToInt32(bsp, 8 + (lump * 8) + 4);
			int relOffset = index * structLen;
			if (relOffset < 0 || relOffset >= length)
				throw new Exception("Attempt to get lump " + lump + " object " + index + " -- this was out of range");
			return offset + relOffset;
		}

		public static int GetBSPStructCount(byte[] bsp, int lump, int structLen) {
			int length = BitConverter.ToInt32(bsp, 8 + (lump * 8) + 4);
			return length / structLen;
		}

		// -- High-Level Getters --

		// Gets a brush side.
		public Plane GetBSPBrushSide(byte[] bsp, int brushSide) {
			int b = GetBSPStructOfs(bsp, 15, brushSide, 4);
			int plane = (int) BitConverter.ToUInt16(bsp, b);
			return GetBSPPlane(bsp, plane);
		}

		// Gets a face winding.
		public List<Vector3> GetBSPFaceWinding(byte[] bsp, int face) {
			int b = GetBSPStructOfs(bsp, 6, face, 20);
			int firstEdge = BitConverter.ToInt32(bsp, b + 4);
			int numEdges = (int) BitConverter.ToUInt16(bsp, b + 8);
			List<Vector3> res = new();
			for (int i = 0; i < numEdges; i++) {
				// has to be mapped using surfedges - see https://github.com/id-Software/Quake-2-Tools/blob/master/bsp/qbsp3/writebsp.c#L213
				int seb = GetBSPStructOfs(bsp, 12, firstEdge + i, 4); // surfedge
				int sebVal = BitConverter.ToInt32(bsp, seb);
				Vector3 tgt = GetBSPEdgeVertex(bsp, sebVal < 0 ? -sebVal : sebVal, sebVal < 0 ? 1 : 0);
				res.Add(tgt);
			}
			return res;
		}

		// Gets a brush side.
		public Vector3 GetBSPEdgeVertex(byte[] bsp, int edge, int vertex) {
			int b = GetBSPStructOfs(bsp, 11, edge, 4);
			int vtx = (int) BitConverter.ToUInt16(bsp, b + (vertex * 2));
			return GetBSPVertex(bsp, vtx);
		}

		// -- Geometric Getters --

		// Gets a plane.
		public Plane GetBSPPlane(byte[] bsp, int plane) {
			int b = GetBSPStructOfs(bsp, 1, plane, 20);
			float nX = BitConverter.ToSingle(bsp, b);
			float nY = BitConverter.ToSingle(bsp, b + 4);
			float nZ = BitConverter.ToSingle(bsp, b + 8);
			float d = BitConverter.ToSingle(bsp, b + 12);
			// [TRANSFORM]
			// So, here's an oddity for you: I don't know why these have to be inverted.
			// They clearly do, so that's a start, but I don't know why.
			return new Plane(new Vector3(-nX, -nZ, -nY), d / -worldScale);
		}
		// Gets a vertex.
		public Vector3 GetBSPVertex(byte[] bsp, int vertex) {
			int b = GetBSPStructOfs(bsp, 2, vertex, 12);
			float nX = BitConverter.ToSingle(bsp, b);
			float nY = BitConverter.ToSingle(bsp, b + 4);
			float nZ = BitConverter.ToSingle(bsp, b + 8);
			// [TRANSFORM]
			// positive X in TB is positive X in Unity
			// positive Y in TB is positive Z in Unity
			// positive Z in TB is positive Y in Unity
			return new Vector3(nX, nZ, nY) / worldScale;
		}

		public struct TriInfo {
			public Vector3 a;
			public Vector2 au;
			public Vector3 b;
			public Vector2 bu;
			public Vector3 c;
			public Vector2 cu;
		}
	}
}
