using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.AssetImporters;
using KDCVRCBSP.ECL;

namespace KDCVRCBSP {
	/// Interface between the ECL and the Unity-related code.
	[ScriptedImporter(1, "map")]
	public class KDCBSPMapImporter : KDCBSPBaseImporter {
		[Tooltip("This feature prevents having to manually export the map from TrenchBroom.")]
		[SerializeField]
		public bool bspSimulateTrenchbroomExport = true;

		[Tooltip("Enables partitioning. This is used for void elimination, etc.")]
		[SerializeField]
		public bool bspDoPartition = true;

		[Tooltip("Indicates that 'leaks' are expected and should not be warned.")]
		[SerializeField]
		public bool bspAllowLeaks = false;

		[Tooltip("Logs info messages.")]
		[SerializeField]
		public bool bspLogInfo = false;

		[Tooltip("Logs info messages.")]
		[SerializeField]
		public bool bspLogWarning = false;

		[Tooltip("Creates debug (obj) files.")]
		[SerializeField]
		public bool bspCreateDebugFiles = false;

		[Tooltip("Creates info (prt) files.")]
		[SerializeField]
		public bool bspCreateInfoFiles = false;

		[Tooltip("Creates warning (pts) files.")]
		[SerializeField]
		public bool bspCreateWarningFiles = false;

		public sealed class Diag: IBSPDiagnostics {
			public KDCBSPMapImporter parent;
			public string outPfx = "/KDCBSP_UTY_BUG";

			void IBSPDiagnostics.Info(string text) {
				if (parent.bspLogInfo)
					Debug.Log("INFO: " + text);
			}

			void IBSPDiagnostics.Warning(string text) {
				if (parent.bspLogWarning)
					Debug.LogWarning(text);
			}

			void IBSPDiagnostics.WriteDiagFileDebug(string filename, Func<List<string>> text) {
				if (parent.bspCreateDebugFiles)
					File.WriteAllLines(outPfx + filename, text());
			}

			void IBSPDiagnostics.WriteDiagFileInfo(string filename, Func<List<string>> text) {
				if (parent.bspCreateInfoFiles)
					File.WriteAllLines(outPfx + filename, text());
			}

			void IBSPDiagnostics.WriteDiagFileWarning(string filename, Func<List<string>> text) {
				if (parent.bspCreateWarningFiles)
					File.WriteAllLines(outPfx + filename, text());
			}
		}

		public class MaterialMemo : IBSPMaterial {
			public readonly string name;
			public readonly KDCBSPAbstractMaterialConfig material;
			public MaterialMemo(string name, KDCBSPAbstractMaterialConfig material) {
				this.name = name;
				this.material = material;
			}
			public BSPSurfaceFlags SurfaceFlags => material.SurfaceFlags;
			public BSPSurfaceFlags TransFlags => material.TransFlags;
		}

		public override KDCBSPIntermediate CompileToIntermediate(KDCBSPImportContext importContext, string assetPath) {
			Dictionary<string, MaterialMemo> memo = new();
			var parsedEntities = MapParser.Parse<MaterialMemo>(File.ReadAllText(assetPath), (name) => {
				if (memo.ContainsKey(name))
					return memo[name];
				var memoRes = new MaterialMemo(name, importContext.LookupMaterial(name));
				memo[name] = memoRes;
				return memoRes;
			});

			if (bspSimulateTrenchbroomExport)
				TrenchBroom.FullSimulateExport(parsedEntities);

			var worldspawn = EntityParsed<MaterialMemo>.EnsureWorldspawn(parsedEntities);

			// 'modern pipeline'
			var map = BSPHighLevel.Act1_MapIntoGeo2(parsedEntities);
			BSPHighLevel.Act2_CompileAll(map, (entity) => {
				return true;
			}, bspDoPartition, bspAllowLeaks, new Diag {
				parent = this,
				outPfx = assetPath + "~"
			});
			BSPHighLevel.Act3_Postprocess(map);

			// compilation complete, now turn this into the intermediate somehow.
			float worldScale = importContext.workspace.WorldScale;
			KDCBSPIntermediate intermediate = new();
			var worldspawnEnt = SetupIntermediateEntity(map.worldspawn, worldScale);
			intermediate.worldspawn = worldspawnEnt;
			intermediate.entities.Add(worldspawnEnt);
			foreach (var ent in map.brushEntities)
				SetupIntermediateEntity(intermediate, ent, worldScale);
			foreach (var ent in map.pointEntities)
				SetupIntermediateEntity(intermediate, ent, worldScale);
			return intermediate;
		}

		private void SetupIntermediateEntity(KDCBSPIntermediate intermediate, Geo2Map<MaterialMemo>.BrushEntity ent, float worldScale) {
			var entComp = SetupIntermediateEntity(ent, worldScale);
			intermediate.entities.Add(entComp);
		}

		private void SetupIntermediateEntity(KDCBSPIntermediate intermediate, EntityKeys ent, float worldScale) {
			var entComp = SetupIntermediateEntity(ent, null, worldScale);
			intermediate.entities.Add(entComp);
		}

		private KDCBSPIntermediate.Entity SetupIntermediateEntity(Geo2Map<MaterialMemo>.BrushEntity ent, float worldScale) {
			KDCBSPIntermediate.Model model = new();
			// For now just ignore the concept of areas and use a dodgy hack
			List<KDCBSPIntermediate.Face> facesAccum = new();
			foreach (var area in ent.areas) {
				foreach (var renderFace in area.renderFaces) {
					var winding = new (Vector3, Vector2)[renderFace.polygon.Count];
					for (int i = 0; i < winding.Length; i++) {
						var src = renderFace.polygon[i];
						var pos = KDCBSPUtilities.TransformPosition((float) src.Item1.x, (float) src.Item1.y, (float) src.Item1.z, worldScale);
						var uv = new Vector2((float) src.Item2.x, (float) -src.Item2.y);
						winding[i] = (pos, uv);
					}
					facesAccum.Add(new KDCBSPIntermediate.Face {
						tex = renderFace.material.name,
						winding = winding
					});
				}
			}
			model.faces = facesAccum.ToArray();
			model.brushes = new KDCBSPIntermediate.Brush[ent.brushes.Count];
			for (int i = 0; i < model.brushes.Length; i++) {
				var ip = ent.brushes[i];
				var op = new KDCBSPIntermediate.Brush();
				op.illusionary = ip.Item1.Illusionary;
				var iConvex = ip.Item2;
				var cbs = new KDCBSPIntermediate.BrushSide[iConvex.faces.Count];
				for (int j = 0; j < cbs.Length; j++) {
					var iSide = iConvex.faces[j];
					var plane = iSide.Plane;
					cbs[j] = new KDCBSPIntermediate.BrushSide {
						plane = KDCBSPUtilities.TransformPlane((float) plane.normal.x, (float) plane.normal.y, (float) plane.normal.z, (float) plane.distance, worldScale),
						texInfo = KDCBSPUtilities.TransformBrushUV(iSide.data.material.name, iSide.data.texUV, worldScale)
					};
				}
				op.sides = cbs;
				model.brushes[i] = op;
			}
			return SetupIntermediateEntity(ent.pairs, model, worldScale);
		}

		private KDCBSPIntermediate.Entity SetupIntermediateEntity(EntityKeys ent, KDCBSPIntermediate.Model model, float worldScale) {
			Vector3d statedOriginD = ent.GetVector3d("origin", Vector3d.Zero);
			Vector3 origin = KDCBSPUtilities.TransformPosition((float) statedOriginD.x, (float) statedOriginD.y, (float) statedOriginD.z, worldScale);
			return new KDCBSPIntermediate.Entity(ent, worldScale, model, origin);
		}
	}
}
