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

			bool kwBSP = bsp[1] == (byte) 'B' && bsp[2] == (byte) 'S' && bsp[3] == (byte) 'P';
			int version = BitConverter.ToInt32(bsp, 4);
			if (bsp[0] == (byte) 'I' && kwBSP && version == 38) {
				return ECLQ2Loader.Load(bsp, false);
			} else if (bsp[0] == (byte) 'Q' && kwBSP && version == 38) {
				return ECLQ2Loader.Load(bsp, true);
			} else {
				throw new Exception("Doesn't look like a supported BSP file (Quake 2 with possible qbism extensions)");
			}
		}

		public class Entity : EntityKeys {
			public Model model;
			public Vector3d origin;

			public Entity(IReadOnlyList<(string, string)> source, Model model, Vector3d origin) {
				this.model = model;
				this.origin = origin;
				foreach (var pair in source)
					Add(pair);

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
			/// Areas are distinct meshes.
			public List<List<ModelRenderable>> areas = new();
			public Vector3d min, max;
			public List<Brush> brushes = new();
			public List<List<Plane3d>> viewLeaves = new();

			public void AddRenderable(ModelRenderable renderable, int area, Dictionary<int, List<ModelRenderable>> areaTable) {
				if (areaTable.TryGetValue(area, out var renderables)) {
					renderables.Add(renderable);
				} else {
					List<ModelRenderable> ls = new();
					ls.Add(renderable);
					areas.Add(ls);
					areaTable[area] = ls;
				}
			}

			public void Translate(Vector3d t) {
				min += t;
				max += t;
				foreach (var area in areas)
					foreach (var renderable in area)
						renderable.Translate(t);
				foreach (var brush in brushes)
					brush.Translate(t);
				foreach (var planeList in viewLeaves) {
					for (int i = 0; i < planeList.Count; i++)
						planeList[i] = planeList[i].Translated(t);
				}
			}
		}

		public abstract class ModelRenderable {
			public abstract void Translate(Vector3d t);
		}

		public struct Vertex {
			public Vector3d position;
			public Vector3d normal;
			public Vector2d uv;
			// q3bsp supports this, but should we? it adds extra VRAM load.
			// I guess have support for loading it and then just conveniently forget it during meshgen
			public byte colourR, colourG, colourB, colourA;
		}

		public class ModelTriangle : ModelRenderable {
			public string tex;
			public Vertex a, b, c;

			public override void Translate(Vector3d t) {
				a.position += t;
				b.position += t;
				c.position += t;
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
