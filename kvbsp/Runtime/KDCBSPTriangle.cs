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
	}
}
