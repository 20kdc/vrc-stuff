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

			public List<Convex3d<bool>> IntoOcclusionGeometry(double occlusionBorder, double worldBorder) {
				bool debugOccy = false;
				if (debugOccy)
					Console.WriteLine(" leaf count: " + viewLeaves.Count);
				// occluder geo time
				Plane3d GenAxialPlane(AABB3d bounds, int id, double nudge) {
					double src;
					Vector3d normal;
					bool flip = false;
					if (id == 0) {
						normal = new Vector3d(1, 0, 0);
						src = bounds.max.x + nudge;
					} else if (id == 1) {
						normal = new Vector3d(-1, 0, 0);
						src = bounds.min.x - nudge;
						flip = true;
					} else if (id == 2) {
						normal = new Vector3d(0, 1, 0);
						src = bounds.max.y + nudge;
					} else if (id == 3) {
						normal = new Vector3d(0, -1, 0);
						src = bounds.min.y - nudge;
						flip = true;
					} else if (id == 4) {
						normal = new Vector3d(0, 0, 1);
						src = bounds.max.z + nudge;
					} else {
						normal = new Vector3d(0, 0, -1);
						src = bounds.min.z - nudge;
						flip = true;
					}
					return new Plane3d(normal, flip ? -src : src);
				}
				var initiator = Convex3d<bool>.FromPlanes(new Plane3d[] {
					bounds.GenAxialPlane(0, worldBorder),
					bounds.GenAxialPlane(1, worldBorder),
					bounds.GenAxialPlane(2, worldBorder),
					bounds.GenAxialPlane(3, worldBorder),
					bounds.GenAxialPlane(4, worldBorder),
					bounds.GenAxialPlane(5, worldBorder),
				}, false, 0.01d, 65536d, true);
				List<Convex3d<bool>> occluderGeo = new();
				if (initiator == null)
					return occluderGeo;
				occluderGeo.Add(initiator);
				List<int> leafIndices = new();
				Convex3d<bool>[] leafConvexes = new Convex3d<bool>[viewLeaves.Count];
				for (int i = 0; i < viewLeaves.Count; i++) {
					leafIndices.Add(i);
					var leaf = viewLeaves[i];
					var preBuildUnexpanded = Convex3d<bool>.FromPlanes(leaf, false, 0.01d, 65536d, true);
					// if this fails, give up now on this leaf.
					if (preBuildUnexpanded == null)
						continue;
					Plane3d[] expanded = new Plane3d[leaf.Length + 6];
					for (int j = 0; j < leaf.Length; j++)
						expanded[j] = new Plane3d(leaf[j].normal, leaf[j].distance + occlusionBorder);
					// axial bevelling
					for (int j = 0; j < 6; j++)
						expanded[leaf.Length + j] = GenAxialPlane(preBuildUnexpanded.bounds, j, occlusionBorder);
					leafConvexes[i] = Convex3d<bool>.FromPlanes(expanded, false, 0.01d, 65536d, true);
				}
				leafIndices.Sort((a, b) => {
					var ca = viewLeaves[a].Length;
					var cb = viewLeaves[b].Length;
					if (ca < cb)
						return 1;
					else if (ca > cb)
						return -1;
					else
						return 0;
				});
				int processed = 0;
				foreach (var leafIndex in leafIndices) {
					var leaf = viewLeaves[leafIndex];
					if (debugOccy)
						Console.WriteLine(" " + processed + " ocg count: " + occluderGeo.Count);
					processed++;
					var tmp = leafConvexes[leafIndex];
					if (tmp == null || tmp.faces.Count < 1)
						continue;
					List<Convex3d<bool>> newGeo = new();
					foreach (var occyUnderTest in occluderGeo) {
						// broadphase
						if (!occyUnderTest.bounds.Intersects(tmp.bounds, 1d)) {
							newGeo.Add(occyUnderTest);
							continue;
						}
						// check if actually intersecting
						bool completeEscape = false;
						foreach (var face in tmp.faces) {
							var (below, above) = occyUnderTest.Cut(face.plane, false);
							if (below == null) {
								completeEscape = true;
								break;
							}
						}
						if (completeEscape) {
							newGeo.Add(occyUnderTest);
							continue;
						}
						// alright, it's intersecting, eliminate
						var occyHold = occyUnderTest;
						foreach (var face in tmp.faces) {
							if (occyHold == null)
								break;
							var (below, above) = occyHold.Cut(face.plane, false);
							if (above != null)
								newGeo.Add(above);
							occyHold = below;
						}
						// occyHold is within the leaf and is thus discarded.
					}
					occluderGeo = newGeo;
				}
				return occluderGeo;
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
