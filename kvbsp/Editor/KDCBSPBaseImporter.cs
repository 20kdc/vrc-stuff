using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEditor;
using UnityEditor.AssetImporters;
using KDCVRCBSP.ECL;

using FlagMod = KDCVRCBSP.KDCBSPBrushEntitySettings.FlagMod;
using CollisionMode = KDCVRCBSP.KDCBSPBrushEntitySettings.CollisionMode;

namespace KDCVRCBSP {
	public abstract class KDCBSPBaseImporter : ScriptedImporter {

		[Tooltip("Maps material names to Unity materials, among other things.")]
		[SerializeField]
		public LazyLoadReference<KDCBSPAbstractWorkspaceConfig> workspace;

		[Tooltip("Brush entity settings for compiling worldspawn (unless otherwise overridden by parameterizers or properties).")]
		[SerializeField]
		public KDCBSPBrushEntitySettings worldspawnCompilation = new();

		public abstract ECLBSPFile CompileToIntermediate(KDCBSPImportContext importContext, string assetPath);

		public override void OnImportAsset(AssetImportContext ctx) {
			if (!workspace.isSet) {
				// yes this is modification and Bad but it makes things make more sense really
				workspace = (KDCBSPAbstractWorkspaceConfig) AssetDatabase.LoadAssetAtPath("Assets/KDCBSPGameRoot/DefaultWorkspaceConfig.asset", typeof(KDCBSPWorkspaceConfig));
			}

			KDCBSPImportContext importContext = new KDCBSPImportContext {
				importer = this,
				assetImportContext = ctx
			};

			var myWorkspace = importContext.DependsOnArtifact<KDCBSPAbstractWorkspaceConfig>(workspace);
			importContext.workspace = myWorkspace;

			List<KDCBSPAbstractWorkspaceConfig> searchOrder = myWorkspace.PrepareSearchOrder(importContext);
			importContext.searchOrder = searchOrder;

			importContext.bsp = CompileToIntermediate(importContext, assetPath);

			GameObject mapGO = KDCBSPImportFlow.BuildMap(importContext, worldspawnCompilation);

			ctx.AddObjectToAsset("main obj", mapGO);
			ctx.SetMainObject(mapGO);
		}
	}
}
