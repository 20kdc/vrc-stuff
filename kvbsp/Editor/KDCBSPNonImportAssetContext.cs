using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace KDCVRCBSP {
	/**
	 * Asset context universal between editor and runtime uses.
	 */
	public class KDCBSPNonImportAssetContext : IKDCBSPAssetContext {
		public T DependsOnArtifact<T>(LazyLoadReference<T> obj) where T: UnityEngine.Object {
			return obj.asset;
		}

		public T DependsOnArtifact<T>(string path) where T: UnityEngine.Object {
			return (T) AssetDatabase.LoadAssetAtPath(path, typeof(T));
		}
	}
}
