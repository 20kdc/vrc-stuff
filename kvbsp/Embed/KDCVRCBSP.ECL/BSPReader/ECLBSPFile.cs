using System;
using System.IO;
using System.Text;
using System.Collections.Generic;

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
			} else {
				throw new Exception("Doesn't look like a supported BSP file (Quake 2 with possible qbism extensions)");
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
					Vector3d newOrigin = (model.min + model.max) / 2;
					Vector3d internalTranslation = origin - newOrigin;
					model.Translate(internalTranslation);
					this.origin = newOrigin;
				}
			}
		}

		public class Model {
			/// Distinct renderables are used when mesh separation is required.
			public List<ModelRenderable> renderables = new();
			public Vector3d min, max;
			public List<Brush> brushes = new();
			public List<List<Plane3d>> viewLeaves = new();

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

			public void Translate(Vector3d t) {
				min += t;
				max += t;
				foreach (var renderable in renderables)
					renderable.Translate(t);
				foreach (var brush in brushes)
					brush.Translate(t);
				foreach (var planeList in viewLeaves)
					for (int i = 0; i < planeList.Count; i++)
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

		public class Brush {
			public bool illusionary;
			public BrushSide[] sides;

			public void Translate(Vector3d t) {
				foreach (var side in sides)
					side.Translate(t);
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
