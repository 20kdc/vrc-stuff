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

			public void AddTri(string tex, (Vertex, Vertex, Vertex) tri, int area, Dictionary<int, Dictionary<string, ModelTriMesh>> table) {
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
				mesh.tris.Add(tri);
			}

			public void AddPolygon(string tex, ECLBSPFile.Vertex[] winding, int area, Dictionary<int, Dictionary<string, ModelTriMesh>> table) {
				Vector3d[] positions = new Vector3d[winding.Length];
				for (int i = 0; i < positions.Length; i++)
					positions[i] = winding[i].position;
				foreach ((int a, int b, int c) in GeomUtil.TriangulateConvexPolygon(positions, 0.01d))
					AddTri(tex, (winding[a], winding[b], winding[c]), area, table);
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

		public struct Vertex {
			public Vector3d position;
			public Vector3d normal;
			public Vector2d uv;
			// q3bsp supports this, but should we? it adds extra VRAM load.
			// I guess have support for loading it and then just conveniently forget it during meshgen
			public byte colourR, colourG, colourB, colourA;

			public static Vertex BezierEval(Vertex a, Vertex b, Vertex c, double pos) {
				// Determine weights.
				double posInv = 1 - pos;
				// combined effects of all lines
				double weightA = posInv * posInv;
				double weightB = pos * posInv * 2;
				double weightC = pos * pos;
				// for i = 0, 10 do v = i / 10 print((v * (1 - v))) end
				// Add AB weight. Left side is vertex weight. Right side is line weight.
				// weightA += posInv * posInv;
				// weightB += pos * posInv;
				// Add BC weight.
				// weightB += posInv * pos;
				// weightC += pos * pos;

				// Determine 'trivial' weights.
				double trivialA = Math.Max(1 - (pos * 2), 0);
				double trivialB = 1 - (Math.Abs(pos - 0.5) * 2);
				double trivialC = Math.Max((pos * 2) - 1, 0);

				byte TrivialInterpolateColourByte(byte ba, byte bb, byte bc) {
					double r = (ba * trivialA) + (bb * trivialB) + (bc * trivialC);
					return (byte) Math.Min(Math.Max(Math.Round(r), 0), 255);
				}

				return new Vertex {
					position = (a.position * weightA) + (b.position * weightB) + (c.position * weightC),
					normal = ((a.normal * weightA) + (b.normal * weightB) + (c.normal * weightC)).Normalized,
					uv = (a.uv * trivialA) + (b.uv * trivialB) + (c.uv * trivialC),
					colourR = TrivialInterpolateColourByte(a.colourR, b.colourR, c.colourR),
					colourG = TrivialInterpolateColourByte(a.colourG, b.colourG, c.colourG),
					colourB = TrivialInterpolateColourByte(a.colourB, b.colourB, c.colourB),
					colourA = TrivialInterpolateColourByte(a.colourA, b.colourA, c.colourA),
				};
			}
		}

		public abstract class ModelRenderable {
			public string tex;
			/// Translates this renderable.
			public abstract void Translate(Vector3d t);
			/// Returns a list of triangles.
			/// Notably, if/when LOD is introduced (for q3 patches), this is where that would be selected.
			public abstract IReadOnlyList<(Vertex, Vertex, Vertex)> Build();
		}

		public class ModelTriMesh : ModelRenderable {
			public List<(Vertex, Vertex, Vertex)> tris = new();

			public override void Translate(Vector3d t) {
				for (int i = 0; i < tris.Count; i++) {
					var tri = tris[i];
					tri.Item1.position += t;
					tri.Item2.position += t;
					tri.Item3.position += t;
					tris[i] = tri;
				}
			}

			public override IReadOnlyList<(Vertex, Vertex, Vertex)> Build() {
				return tris;
			}
		}

		public class ModelQ3Patch : ModelRenderable {
			public Vertex[,] grid;

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

			public Vertex Resolve(int x, int y, int resW, int resH) {
				// In this coordinate system, the lower-right edge belongs to the next patch.
				int inPatchX = x % (resW + 1);
				int inPatchY = y % (resH + 1);
				double polX = ((double) inPatchX) / (resW + 1);
				double polY = ((double) inPatchY) / (resH + 1);
				int patchX = x / (resW + 1);
				int patchY = y / (resH + 1);
				int baseX = patchX * 2;
				int baseY = patchY * 2;
				bool flightX = baseX >= (grid.GetLength(0) - 1);
				bool flightY = baseY >= (grid.GetLength(1) - 1);
				if (flightX && flightY) {
					return grid[baseX + 0, baseY + 0];
				} else if (flightX) {
					var v00 = grid[baseX + 0, baseY + 0];
					var v01 = grid[baseX + 0, baseY + 1];
					var v02 = grid[baseX + 0, baseY + 2];
					return Vertex.BezierEval(v00, v01, v02, polY);
				} else if (flightY) {
					var v00 = grid[baseX + 0, baseY + 0];
					var v10 = grid[baseX + 1, baseY + 0];
					var v20 = grid[baseX + 2, baseY + 0];
					return Vertex.BezierEval(v00, v10, v20, polX);
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
					var col0 = Vertex.BezierEval(v00, v01, v02, polY);
					var col1 = Vertex.BezierEval(v10, v11, v12, polY);
					var col2 = Vertex.BezierEval(v20, v21, v22, polY);
					return Vertex.BezierEval(col0, col1, col2, polX);
				}
			}

			public override IReadOnlyList<(Vertex, Vertex, Vertex)> Build() {
				List<(Vertex, Vertex, Vertex)> tris = new();
				// Extremely temporary code. Indexed mesh support will be a likely future requirement. - 🟪
				int resolution = 3;
				(int resolvedW, int resolvedH) = ExpandedSizeForResolution(resolution, resolution);
				Vertex[,] resolved = new Vertex[resolvedW, resolvedH];
				Parallel.For(0, resolvedW, (i, _) => {
					for (int j = 0; j < resolvedH; j++)
						resolved[i, j] = Resolve(i, j, resolution, resolution);
				});
				for (int i = 0; i < resolvedW - 1; i++) {
					for (int j = 0; j < resolvedH - 1; j++) {
						tris.Add((
							resolved[i, j],
							resolved[i, j + 1],
							resolved[i + 1, j]
						));
						tris.Add((
							resolved[i + 1, j],
							resolved[i, j + 1],
							resolved[i + 1, j + 1]
						));
					}
				}
				return tris;
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
