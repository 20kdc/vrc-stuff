using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using VRC.SDKBase;

namespace KDCVRCBSP {
	/**
	 * Contains various utilities.
	 */
	public static class KDCBSPUtilities {
		public const string KVBSP_BASE = "Packages/t20kdc.vrc-bsp/";

#if UNITY_EDITOR
		/// Using Unity paths, get bytes or null.
		public static byte[] ReadLPBytesOrNull(string path) {
			try {
				return File.ReadAllBytes(UnityEditor.FileUtil.GetPhysicalPath(path));
#pragma warning disable CS0168
			} catch (Exception _) {
			}
#pragma warning restore CS0168
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
#endif

		public static LayerMask BrushContentsLayerMask(LayerMask entityLayer, int contents) {
			// CONTENTS_CURRENT_0
			// We use this as a 'secret handshake' to implement the 'noclip' brush.
			// Noclip brushes are solid (so block vis), but don't create collision.
			if ((contents & 0x40000) != 0)
				return 0;
			// CONTENTS_SOLID | CONTENTS_PLAYERCLIP
			else if ((contents & (1 | 0x10000)) == 0)
				return 0;
			return entityLayer;
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
	}
}
