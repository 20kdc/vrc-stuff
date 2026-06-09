using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using KDCVRCBSP.ECL;

namespace KDCVRCBSP {
	/// This is being split out of KDCBSPIntermediate because it's independently useful.
	public struct KDCBSPTriangle {
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

		/// Transforms triangles into a mesh.
		public static Mesh TrianglesToMesh(List<KDCBSPTriangle> triangles, Vector2 uvMul) {
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

		/// Converts a brush to triangles.
		public static void BrushToTriangles(ECLBSPFile.Brush brush, List<KDCBSPTriangle> triangles, float worldScale) {
			float epsilon = 0.05f;
			float initQuadSize = 131072;
			for (int i = 0; i < brush.sides.Length; i++) {
				// note this non-Geo2 quick rewrite of the convex cutting code.
				// so sad
				// create initial
				List<Vector3d> winding = GeomUtil.GenInitialWinding(brush.sides[i].plane, initQuadSize);
				// cut
				for (int j = 0; j < brush.sides.Length; j++) {
					if (j == i)
						continue;
					brush.sides[j].plane.CutWinding(winding, null, epsilon);
					if (winding.Count < 3)
						break;
				}
				if (winding.Count < 3)
					continue;

				var side = brush.sides[i];
				// convert from ECL to Unity
				Vector3[] windingConv = new Vector3[winding.Count];
				for (int j = 0; j < windingConv.Length; j++) {
					int revIndex = winding.Count - (j + 1);
					var pos = KDCBSPUtilities.TransformPosition(winding[j], worldScale);
					windingConv[revIndex] = pos;
				}

				var a = windingConv[0];
				for (int j = 1; j < windingConv.Length - 1; j++) {
					var b = windingConv[j];
					var c = windingConv[j + 1];
					triangles.Add(new KDCBSPTriangle {
						a = a,
						au = new Vector2(0, 0),
						b = b,
						bu = new Vector2(0, 0),
						c = c,
						cu = new Vector2(0, 0)
					});
				}
			}
		}
	}
}
