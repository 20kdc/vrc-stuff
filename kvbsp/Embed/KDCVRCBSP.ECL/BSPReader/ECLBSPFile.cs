using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace KDCVRCBSP.ECL {
	/// BSP file for ECL.
	public class ECLBSPFile {
		public Entity worldspawn;
		public List<Entity> entities = new();

		/// Just a proxy for KDCBSPQ2Loader right now.
		public static ECLBSPFile Load(byte[] bsp) {
			if (bsp.Length < 8)
				throw new Exception("Not even long enough for the magic number!");

			int gqVer = BitConverter.ToInt32(bsp, 0);
			if (gqVer == 0x1C || gqVer == 0x1E)
				return ECLQHLLoader.Load(bsp);

			bool kwBSP = bsp[1] == (byte) 'B' && bsp[2] == (byte) 'S' && bsp[3] == (byte) 'P';
			int version = BitConverter.ToInt32(bsp, 4);
			if (bsp[0] == (byte) 'I' && kwBSP && version == 38) {
				// IBSP 38
				return ECLQ2Loader.Load(bsp, false);
			} else if (bsp[0] == (byte) 'Q' && kwBSP && version == 38) {
				// qbism
				return ECLQ2Loader.Load(bsp, true);
			} else if (bsp[0] == (byte) 'I' && kwBSP && (version == 46 || version == 47)) {
				return ECLQ3Loader.Load(bsp);
				// throw new Exception("Quake 3 BSP not yet supported");
			} else {
				throw new Exception("Doesn't look like a supported BSP file (Quake 2 (includes qbism format). Quake 1 & GoldSrc supported with caveats, Quake 3 / Quake Live)");
			}
		}

		public class Entity : EntityKeys {
			public Model model;
			public Vector3d origin;

			public Entity(IReadOnlyList<(string, string)> source, Model model) {
				this.model = model;
				foreach (var pair in source)
					Add(pair);
				origin = GetVector3d("origin", Vector3d.Zero);

				// 'out-of-compiler' autoorigin
				if (model != null && GetBool("_kdcbsp_autoorigin", false)) {
					Vector3d newOrigin = (model.bounds.min + model.bounds.max) / 2;
					Vector3d internalTranslation = origin - newOrigin;
					model.Translate(internalTranslation);
					this.origin = newOrigin;
				}
			}
		}

		public class Model {
			/// Distinct renderables are used when mesh separation is required.
			public List<ModelRenderable> renderables = new();
			public AABB3d bounds;
			public List<Brush> brushes = new();
			public List<Plane3d[]> viewLeaves = new();

			public ModelTriMesh EnsureGeneralTriMesh(string tex, int area, Dictionary<int, Dictionary<string, ModelTriMesh>> table) {
				Dictionary<string, ModelTriMesh> areaTable;
				if (!table.TryGetValue(area, out areaTable)) {
					areaTable = new();
					table[area] = areaTable;
				}
				ModelTriMesh mesh;
				if (!areaTable.TryGetValue(tex, out mesh)) {
					mesh = new();
					mesh.tex = tex;
					areaTable[tex] = mesh;
					renderables.Add(mesh);
				}
				return mesh;
			}

			public void AddTri(string tex, (ECLMesh.Vertex, ECLMesh.Vertex, ECLMesh.Vertex) tri, int area, Dictionary<int, Dictionary<string, ModelTriMesh>> table) {
				ModelTriMesh mesh = EnsureGeneralTriMesh(tex, area, table);
				int baseVtx = mesh.vertices.Count;
				mesh.vertices.Add(tri.Item1);
				mesh.vertices.Add(tri.Item2);
				mesh.vertices.Add(tri.Item3);
				mesh.tris.Add((baseVtx, baseVtx + 1, baseVtx + 2));
			}

			public void AddPolygon(string tex, ECLMesh.Vertex[] winding, int area, Dictionary<int, Dictionary<string, ModelTriMesh>> table) {
				ModelTriMesh mesh = EnsureGeneralTriMesh(tex, area, table);
				int baseVtx = mesh.vertices.Count;
				mesh.vertices.AddRange(winding);
				Vector3d[] positions = new Vector3d[winding.Length];
				for (int i = 0; i < positions.Length; i++)
					positions[i] = winding[i].position;
				foreach ((int a, int b, int c) in GeomUtil.TriangulateConvexPolygon(positions, 0.01d))
					mesh.tris.Add((baseVtx + a, baseVtx + b, baseVtx + c));
			}

			public void Translate(Vector3d t) {
				bounds.min += t;
				bounds.max += t;
				foreach (var renderable in renderables)
					renderable.Translate(t);
				foreach (var brush in brushes)
					brush.Translate(t);
				foreach (var planeList in viewLeaves)
					for (int i = 0; i < planeList.Length; i++)
						planeList[i] = planeList[i].Translated(t);
			}
		}

		public abstract class ModelRenderable {
			public string tex;
			/// Translates this renderable.
			public abstract void Translate(Vector3d t);
			/// Returns a mesh.
			/// Notably, if/when LOD is introduced (for q3 patches), this is where that would be selected.
			/// DO NOT MODIFY.
			public abstract ECLMesh Build();
		}

		public class ModelTriMesh : ModelRenderable {
			public List<ECLMesh.Vertex> vertices = new();
			public List<(int, int, int)> tris = new();

			public override void Translate(Vector3d t) {
				for (int i = 0; i < vertices.Count; i++) {
					var vtx = vertices[i];
					vtx.position += t;
					vertices[i] = vtx;
				}
			}

			public override ECLMesh Build() {
				return new ECLMesh(vertices, tris);
			}
		}

		public class ModelQ3Patch : ModelRenderable {
			public ECLMesh.Vertex[,] grid;

			public override void Translate(Vector3d t) {
				for (int i = 0; i < grid.GetLength(0); i++)
					for (int j = 0; j < grid.GetLength(1); j++)
						grid[i, j].position += t;
			}

			public (int, int) PatchCount {
				get {
					int patchCountW = (grid.GetLength(0) - 1) / 2;
					if (patchCountW < 0)
						patchCountW = 0;
					int patchCountH = (grid.GetLength(1) - 1) / 2;
					if (patchCountH < 0)
						patchCountH = 0;
					return (patchCountW, patchCountH);
				}
			}

			/// The amount of vertices required for the patch at a given resolution.
			/// Resolution is defined here as the number of 'mid-curve' vertices, as first and last vertices are always present.
			public (int, int) ExpandedSizeForResolution(int resW, int resH) {
				(int patchCountW, int patchCountH) = PatchCount;
				return (
					patchCountW == 0 ? 0 : (1 + (patchCountW * (resW + 1))),
					patchCountH == 0 ? 0 : (1 + (patchCountH * (resH + 1)))
				);
			}

			public ECLMesh.Vertex Resolve(int x, int y, int resW, int resH) {
				// In this coordinate system, the lower-right edge belongs to the next patch.
				int inPatchX = x % (resW + 1);
				int inPatchY = y % (resH + 1);
				double polX = ((double) inPatchX) / (resW + 1);
				double polY = ((double) inPatchY) / (resH + 1);
				int patchX = x / (resW + 1);
				int patchY = y / (resH + 1);
				int baseX = patchX * 2;
				int baseY = patchY * 2;
				bool flightX = (baseX >= (grid.GetLength(0) - 2)) || (inPatchX == 0);
				bool flightY = (baseY >= (grid.GetLength(1) - 2)) || (inPatchY == 0);
				if (flightX && flightY) {
					return grid[baseX + 0, baseY + 0];
				} else if (flightX) {
					var v00 = grid[baseX + 0, baseY + 0];
					var v01 = grid[baseX + 0, baseY + 1];
					var v02 = grid[baseX + 0, baseY + 2];
					return ECLMesh.Vertex.BezierEval(v00, v01, v02, polY);
				} else if (flightY) {
					var v00 = grid[baseX + 0, baseY + 0];
					var v10 = grid[baseX + 1, baseY + 0];
					var v20 = grid[baseX + 2, baseY + 0];
					return ECLMesh.Vertex.BezierEval(v00, v10, v20, polX);
				} else {
					var v00 = grid[baseX + 0, baseY + 0];
					var v10 = grid[baseX + 1, baseY + 0];
					var v20 = grid[baseX + 2, baseY + 0];
					var v01 = grid[baseX + 0, baseY + 1];
					var v11 = grid[baseX + 1, baseY + 1];
					var v21 = grid[baseX + 2, baseY + 1];
					var v02 = grid[baseX + 0, baseY + 2];
					var v12 = grid[baseX + 1, baseY + 2];
					var v22 = grid[baseX + 2, baseY + 2];
					var col0 = ECLMesh.Vertex.BezierEval(v00, v01, v02, polY);
					var col1 = ECLMesh.Vertex.BezierEval(v10, v11, v12, polY);
					var col2 = ECLMesh.Vertex.BezierEval(v20, v21, v22, polY);
					return ECLMesh.Vertex.BezierEval(col0, col1, col2, polX);
				}
			}

			public override ECLMesh Build() {
				int resolution = 3;
				(int resolvedW, int resolvedH) = ExpandedSizeForResolution(resolution, resolution);
				ECLMesh.Vertex[] resolved = new ECLMesh.Vertex[resolvedW * resolvedH];
				Parallel.For(0, resolvedW * resolvedH, (i, _) => {
					resolved[i] = Resolve(i % resolvedW, i / resolvedW, resolution, resolution);
				});
				int Idx(int i, int j) {
					return i + (j * resolvedW);
				}
				List<(int, int, int)> tris = new();
				for (int i = 0; i < resolvedW - 1; i++) {
					for (int j = 0; j < resolvedH - 1; j++) {
						tris.Add((
							Idx(i, j),
							Idx(i, j + 1),
							Idx(i + 1, j)
						));
						tris.Add((
							Idx(i + 1, j),
							Idx(i, j + 1),
							Idx(i + 1, j + 1)
						));
					}
				}
				return new(resolved, tris);
			}
		}

		public class Brush {
			public bool illusionary, occyViewpoint;
			public BrushSide[] sides;

			public void Translate(Vector3d t) {
				foreach (var side in sides)
					side.Translate(t);
			}

			public Plane3d[] ToPlanes() {
				Plane3d[] p = new Plane3d[sides.Length];
				for (int i = 0; i < p.Length; i++)
					p[i] = sides[i].plane;
				return p;
			}
		}

		public class BrushSide {
			public Plane3d plane;
			public string tex;
			public BrushUV texUV;

			public void Translate(Vector3d t) {
				plane = plane.Translated(t);
				texUV = texUV.Translated(t);
			}
		}
	}
}
