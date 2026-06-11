using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using KDCVRCBSP.ECL;

namespace KDCVRCBSP {
	/**
	 * Asset context universal between editor and runtime uses.
	 */
	public interface IKDCBSPAssetContext {
		/// DependsOnArtifact and loader.
		public T DependsOnArtifact<T>(LazyLoadReference<T> obj) where T: UnityEngine.Object;
		/// DependsOnArtifact and loader.
		public T DependsOnArtifact<T>(string path) where T: UnityEngine.Object;
	}
}
