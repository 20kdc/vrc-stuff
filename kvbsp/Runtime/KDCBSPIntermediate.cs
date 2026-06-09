using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using KDCVRCBSP.ECL;

namespace KDCVRCBSP {
	/**
	 * The intermediate form used by KDCBSP for import.
	 * If a problem can be simply solved in KDCBSPQ2Loader, it is.
	 * If not, it's solved either here or in KDCBSPImporter.
	 */
	public class KDCBSPIntermediate {
		public Entity worldspawn;

		public Entity Worldspawn => worldspawn;

		/// Parsed entities.
		public List<Entity> entities = new();

		public class Entity : ROEntityKeys {
			// Auto-parsed early
			public readonly string classname;
			public readonly string targetname;
			// Nullable (can have no brush model at all)
			public readonly Model model;
			public readonly float worldScale;
			public readonly Vector3 origin;

			public bool IsWorldspawn => classname == "worldspawn";

			public Entity(EntityKeys sourceKeys, float worldScale, Model model, Vector3 origin) : base(sourceKeys) {
				this.worldScale = worldScale;
				this.model = model;
				string detectedClassname = this["classname"];
				if (detectedClassname == "") {
					classname = "info_unknown";
				} else {
					classname = detectedClassname;
				}
				targetname = this["targetname"];
				// origin may be 'adjusted' by loader
				this.origin = origin;
			}

			public Vector3 GetVector3Position(string key, Vector3 defaultVal) {
				string[] s3 = this[key].Split(' ');
				if (s3.Length == 3)
					if (float.TryParse(s3[0], out var x))
						if (float.TryParse(s3[1], out var y))
							if (float.TryParse(s3[2], out var z))
								return KDCBSPUtilities.TransformPosition(x, y, z, worldScale);
				return defaultVal;
			}
		}

		public class Model {
			public Vector3 mins, maxs;
			public Face[] faces;
			// If explicit UV-mapped face support is required, add 'UVFace' struct and appropriate array here.
			public Brush[] brushes;
		}

		public struct Face {
			// This used to use TexInfo, but this new format is more amiable to various kinds of geometry import.
			// In addition the renderfaces system in the ECL uses the pos+uv layout.
			public string tex;
			public (Vector3, Vector2)[] winding;
		}

		public struct Brush {
			public bool illusionary;
			public BrushSide[] sides;
		}

		public struct BrushSide {
			public Plane plane;
			public string tex;
		}

		// -- Loader Picker --

		/// Now proxies the ECL. The idea is that bits of KDCBSPIntermediate will be switched out with ECL over time.
		public static KDCBSPIntermediate Load(byte[] bsp, float worldScale) {
			var bspFile = ECLBSPFile.Load(bsp);
			KDCBSPIntermediate intermediate = new();
			foreach (var entity in bspFile.entities) {
				Model model = null;
				if (entity.model != null) {
					model = new();
					model.mins = KDCBSPUtilities.TransformPosition(entity.model.min, worldScale);
					model.maxs = KDCBSPUtilities.TransformPosition(entity.model.max, worldScale);
					List<Face> faces = new();
					List<Brush> brushes = new();
					foreach (var area in entity.model.areas) {
						foreach (var srcRenderable in area) {
							if (srcRenderable is ECLBSPFile.ModelTriangle tri) {
								Face nf = new();
								nf.tex = tri.tex;
								(Vector3, Vector2) ConvVtx(ECLBSPFile.Vertex vtx) {
									return (
										KDCBSPUtilities.TransformPosition(vtx.position, worldScale),
										new Vector2((float) vtx.uv.x, 1 - (float) vtx.uv.y)
									);
								}
								nf.winding = new (Vector3, Vector2)[] {
									ConvVtx(tri.a),
									ConvVtx(tri.b),
									ConvVtx(tri.c)
								};
								faces.Add(nf);
							}
						}
					}
					foreach (var srcBrush in entity.model.brushes) {
						Brush dstBrush = new();
						dstBrush.illusionary = srcBrush.illusionary;
						dstBrush.sides = new BrushSide[srcBrush.sides.Length];
						for (int i = 0; i < srcBrush.sides.Length; i++) {
							dstBrush.sides[i] = new BrushSide {
								tex = srcBrush.sides[i].tex,
								plane = KDCBSPUtilities.TransformPlane(srcBrush.sides[i].plane, worldScale),
							};
						}
						brushes.Add(dstBrush);
					}
					model.faces = faces.ToArray();
					model.brushes = brushes.ToArray();
				}
				var ime = new Entity(entity, worldScale, model, KDCBSPUtilities.TransformPosition(entity.origin, worldScale));
				intermediate.entities.Add(ime);
				if (entity == bspFile.worldspawn)
					intermediate.worldspawn = ime;
			}
			return intermediate;
		}

		// -- Loader Assist --

		public void GetEntityBox(Entity entity, out Vector3 centre, out Vector3 size) {
			centre = Vector3.zero;
			size = Vector3.zero;
			var mdl = entity.model;
			if (mdl == null)
				return;
			var bspCentre = (mdl.mins + mdl.maxs) / 2;
			centre = bspCentre;
			size = Vector3.Max(mdl.maxs, mdl.mins) - Vector3.Min(mdl.maxs, mdl.mins);
		}

		// -- Windings --

		/// Adds this face's triangles.
		public void FaceToTriangles(Face f, List<KDCBSPTriangle> targetList) {
			var a = f.winding[0];
			for (int j = 1; j < f.winding.Length - 1; j++) {
				var b = f.winding[j];
				var c = f.winding[j + 1];
				targetList.Add(new KDCBSPTriangle {
					a = a.Item1,
					au = a.Item2,
					b = b.Item1,
					bu = b.Item2,
					c = c.Item1,
					cu = c.Item2
				});
			}
		}

		/// Adds this face's triangles, annotated and UV'd with texture name.
		public void FaceToTriangles(Face f, List<(KDCBSPTriangle, string)> targetList) {
			var a = f.winding[0];
			for (int j = 1; j < f.winding.Length - 1; j++) {
				var b = f.winding[j];
				var c = f.winding[j + 1];
				targetList.Add((new KDCBSPTriangle {
					a = a.Item1,
					au = a.Item2,
					b = b.Item1,
					bu = b.Item2,
					c = c.Item1,
					cu = c.Item2
				}, f.tex));
			}
		}

		// -- Convex Slicer --

		/// Converts a brush to faces.
		public List<Face> BrushToFaces(Brush brush, float worldScale) {
			Plane3d[] planes = new Plane3d[brush.sides.Length];
			for (int i = 0; i < brush.sides.Length; i++)
				planes[i] = KDCBSPUtilities.ToECL(brush.sides[i].plane);
			List<Face> res = new();
			float epsilon = 0.05f / worldScale;
			float initQuadSize = 131072 / worldScale;
			for (int i = 0; i < brush.sides.Length; i++) {
				// note this non-Geo2 quick rewrite of the convex cutting code.
				// so sad
				// create initial
				List<Vector3d> winding = GeomUtil.GenInitialWinding(planes[i], initQuadSize);
				// cut
				for (int j = 0; j < brush.sides.Length; j++) {
					if (j == i)
						continue;
					planes[j].CutWinding(winding, null, epsilon);
					if (winding.Count < 3)
						break;
				}
				if (winding.Count < 3)
					continue;

				var side = brush.sides[i];
				// convert from ECL to Unity and add fake UVs (this is only used for collision)
				(Vector3, Vector2)[] windingConv = new (Vector3, Vector2)[winding.Count];
				for (int j = 0; j < windingConv.Length; j++) {
					int revIndex = winding.Count - (j + 1);
					var pos = KDCBSPUtilities.FromECL(winding[j]);
					windingConv[revIndex] = (pos, new Vector2(0, 0));
				}

				res.Add(new Face {
					tex = side.tex,
					winding = windingConv
				});
			}
			return res;
		}
	}
}
