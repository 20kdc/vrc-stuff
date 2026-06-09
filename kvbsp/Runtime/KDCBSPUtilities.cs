using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using VRC.SDKBase;
using KDCVRCBSP.ECL;

namespace KDCVRCBSP {
	/**
	 * Contains various utilities.
	 */
	public static class KDCBSPUtilities {
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
		public static void UpdatePAKFile(string basePhysicalPath, byte[] content) {
			foreach (string victim in Directory.GetFiles(basePhysicalPath, ".cache*.pk3")) {
				try {
					File.Delete(victim);
				} catch (Exception ex) {
					Debug.LogException(ex);
				}
			}
			string tsPadded = Convert.ToString(DateTimeOffset.UtcNow.ToUnixTimeSeconds(), 16).PadLeft(16, '0');
			string newFileName = ".cache" + tsPadded + ".pk3";
			File.WriteAllBytes(Path.Join(basePhysicalPath, newFileName), content);
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

		// -- Loader Assist --

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

		// -- FromECL conv --

		public static Vector2 FromECL(Vector2d src) {
			return new Vector2((float) src.x, (float) src.y);
		}

		public static Vector3 FromECL(Vector3d src) {
			return new Vector3((float) src.x, (float) src.y, (float) src.z);
		}

		public static Plane FromECL(Plane3d plane) {
			// Note the inversion of distance.
			// The ECL uses the same plane definition as Godot and ID (N=(1,0,0) D=32 means the X+ plane at (32,0,0))
			// Unity's plane definition is such that the same input would mean an X+ plane at (-32,0,0).
			return new Plane(FromECL(plane.normal), (float) -plane.distance);
		}

		// -- ToECL conv --

		public static Vector2d ToECL(Vector2 src) {
			return new Vector2d(src.x, src.y);
		}

		public static Vector3d ToECL(Vector3 src) {
			return new Vector3d(src.x, src.y, src.z);
		}

		public static Plane3d ToECL(Plane plane) {
			// See FromECL for rationale
			return new Plane3d(ToECL(plane.normal), -plane.distance);
		}
	}
}
