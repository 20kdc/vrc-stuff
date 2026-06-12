using System;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using KDCVRCBSP.ECL;

namespace KDCVRCBSP {
	/**
	 * Contains various utilities.
	 */
	public static class KDCBSPUtilities {
		public const double DistanceEpsilon = 1d / 256d;
		public const double InitialWindingSize = 65536d;

		public const string KVBSP_BASE = "Packages/t20kdc.vrc-bsp/";

		/// Using Unity paths, get bytes or null.
		public static byte[] ReadLPBytesOrNull(string path) {
#if UNITY_EDITOR
			try {
				return File.ReadAllBytes(UnityEditor.FileUtil.GetPhysicalPath(path));
#pragma warning disable CS0168
			} catch (Exception _) {
			}
#pragma warning restore CS0168
#endif
			return null;
		}

		/// Reads a texture in a CPU-readable form from a Unity path.
		public static Texture2D ReadLPImageOrNull(string path) {
			byte[] bytes = ReadLPBytesOrNull(path);
			if (bytes == null)
				return null;
			var res = new Texture2D(2, 2);
			if (ImageConversion.LoadImage(res, bytes, false))
				return res;
			return null;
		}

		public static Texture2D ReadRenderTexture(RenderTexture rt) {
			int width = rt.width;
			int height = rt.height;
			var t2d = new Texture2D(width, height);
			// prepare for grab
			var prev = RenderTexture.active;
			RenderTexture.active = rt;
			t2d.ReadPixels(new Rect(0, 0, width, height), 0, 0);
			t2d.Apply();
			RenderTexture.active = prev;
			return t2d;
		}

		public static KDCBSPRuntimeStaticEditorFlags GetStaticEditorFlags(GameObject go) {
#if UNITY_EDITOR
			return (KDCBSPRuntimeStaticEditorFlags) UnityEditor.GameObjectUtility.GetStaticEditorFlags(go);
#else
			return 0;
#endif
		}

		public static void SetStaticEditorFlags(GameObject go, KDCBSPRuntimeStaticEditorFlags flags) {
#if UNITY_EDITOR
			UnityEditor.GameObjectUtility.SetStaticEditorFlags(go, (UnityEditor.StaticEditorFlags) flags);
#endif
		}

		/// So a problem is that TrenchBroom will hold a PAK file open for as long as it wants and expects it not to change.
		/// If you change it anyway, bad things happen.
		/// This is not to mention the Windows file exclusion issues this risks.
		/// So in order to prevent this, we write the next PAK file into a new file, and try to delete other files.
		/// We also name our PAK file by time to control precedence.
		public static void UpdateVFS(string basePhysicalPath, SortedDictionary<string, byte[]> files) {
			string vfsDir = Path.Join(basePhysicalPath, "baseq3");
			try {
				Directory.CreateDirectory(vfsDir);
			} catch (Exception ex) {
				Debug.LogException(ex);
			}
			foreach (string victim in Directory.GetFiles(vfsDir, ".cache*.pk3")) {
				try {
					File.Delete(victim);
				} catch (Exception ex) {
					Debug.LogException(ex);
				}
			}
			string tsPadded = Convert.ToString(DateTimeOffset.UtcNow.ToUnixTimeSeconds(), 16).PadLeft(16, '0');
			string newFileName = ".cache" + tsPadded + ".pk3";
			// pk3 is a fancy term for zip
			var memStream = new MemoryStream();
			var zip = new ZipArchive(memStream, ZipArchiveMode.Create, true, new UTF8Encoding(false));
			foreach (var (key, value) in files) {
				var entry = zip.CreateEntry(key);
				using (Stream stream = entry.Open()) {
					stream.Write(value, 0, value.Length);
				}
			}
			zip.Dispose();
			var content = memStream.ToArray();
			File.WriteAllBytes(Path.Join(vfsDir, newFileName), content);
		}

		public static int LayerMaskToLayer(LayerMask lm) {
			uint lmi = (uint) (int) lm;
			if (lmi == 0)
				return -1;
			int layer = 0;
			while (lmi != 0) {
				lmi >>= 1;
				layer++;
			}
			return layer;
		}

		// -- Main import assist --

		public static void GetEntityBox(ECLBSPFile.Entity entity, float worldScale, out Vector3 centre, out Vector3 size) {
			centre = Vector3.zero;
			size = Vector3.zero;
			var mdl = entity.model;
			if (mdl == null)
				return;
			var minT = TransformPosition(mdl.bounds.min, worldScale);
			var maxT = TransformPosition(mdl.bounds.max, worldScale);
			centre = (minT + maxT) / 2;
			size = Vector3.Max(maxT, minT) - Vector3.Min(maxT, minT);
		}

		public static Mesh ImportECLMeshCore(ECLMesh mesh, bool visual, Vector2 uvMul, float worldScale, bool forceIndex32) {
			var vertices = new Vector3[mesh.vertices.Count];
			var normals = visual ? new Vector3[mesh.vertices.Count] : null;
			var uvs = visual ? new Vector2[mesh.vertices.Count] : null;
			var indices = new int[mesh.triangles.Count * 3];

			for (int i = 0; i < mesh.vertices.Count; i++) {
				var v = mesh.vertices[i];
				// [TRANSFORM] hand-inline just in case
				vertices[i] = new Vector3((float) v.position.x, (float) v.position.z, (float) v.position.y) / worldScale;
				if (visual) {
					normals[i] = new Vector3((float) v.normal.x, (float) v.normal.z, (float) v.normal.y);
					uvs[i] = new Vector2((float) v.uv.x, (float) v.uv.y) * uvMul;
				}
			}

			for (int j = 0; j < mesh.triangles.Count; j++) {
				int b = j * 3;
				var tri = mesh.triangles[j];
				indices[b] = tri.Item1;
				indices[b + 1] = tri.Item2;
				indices[b + 2] = tri.Item3;
			}

			Mesh res = new Mesh();

			// don't want to accidentally hit primitive restart
			if (forceIndex32 || indices.Length >= 65535) {
				res.indexFormat = IndexFormat.UInt32;
			} else {
				res.indexFormat = IndexFormat.UInt16;
			}

			res.vertices = vertices;
			if (visual) {
				res.normals = normals;
				res.uv = uvs;
			}

			res.triangles = indices;

			if (!visual)
				res.RecalculateNormals();
			else
				res.RecalculateTangents();
			res.Optimize();
			return res;
		}

		public static Mesh ImportECLMeshCollision(ECLMesh mesh, float worldScale) {
			return ImportECLMeshCore(mesh, false, Vector2.one, worldScale, false);
		}

		public static Mesh ImportECLMeshVisual(ECLMesh mesh, Vector2 uvMul, float worldScale, KDCBSPBrushEntitySettings lightmapSettings) {
			Mesh res = ImportECLMeshCore(mesh, true, uvMul, worldScale, false);
#if UNITY_EDITOR
			// Unwrapping is costly, so we should only do it if this is a visual mesh and the lightmap scale is set.
			if (lightmapSettings.lightmapScale > 0) {
				UnityEditor.UnwrapParam.SetDefaults(out UnityEditor.UnwrapParam unwrapParam);
				unwrapParam.packMargin = lightmapSettings.lightmapPackMargin;
				// try twice with upgrade on fail
				if (!UnityEditor.Unwrapping.GenerateSecondaryUVSet(res, unwrapParam)) {
					if (res.indexFormat != IndexFormat.UInt32) {
						res = ImportECLMeshCore(mesh, true, uvMul, worldScale, true);
						UnityEditor.Unwrapping.GenerateSecondaryUVSet(res, unwrapParam);
					}
				}
			}
#endif
			return res;
		}

		// -- Transform functions --

		public static Plane TransformPlane(Plane3d plane, float worldScale) {
			return TransformPlane((float) plane.normal.x, (float) plane.normal.y, (float) plane.normal.z, (float) plane.distance, worldScale);
		}

		public static Plane TransformPlane(float nX, float nY, float nZ, float d, float worldScale) {
			// [TRANSFORM]
			// So, here's an oddity for you: I don't know why distance has to be inverted.
			// It clearly does, so that's a start, but I don't know why.
			// UPDATE: The reason is because Unity's definition of `distance` is bad.
			// One would think that a point on P exists at (N * D).
			// However, according to the field doc for `distance`, by Unity logic, it is actually at (N * -D).
			return new Plane(new Vector3(nX, nZ, nY), d / -worldScale);
		}

		public static Vector3 TransformPosition(Vector3d src, float worldScale) {
			return TransformPosition((float) src.x, (float) src.y, (float) src.z, worldScale);
		}

		public static Vector3 TransformNormal(Vector3d src) {
			return new Vector3((float) src.x, (float) src.z, (float) src.y);
		}

		public static Vector3 TransformPosition(float nX, float nY, float nZ, float worldScale) {
			// [TRANSFORM]
			// positive X in TB is positive X in Unity
			// positive Y in TB is positive Z in Unity
			// positive Z in TB is positive Y in Unity
			return new Vector3(nX, nZ, nY) / worldScale;
		}
	}
}
