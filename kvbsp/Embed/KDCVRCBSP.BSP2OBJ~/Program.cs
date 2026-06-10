using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using KDCVRCBSP.ECL;

namespace KDCVRCBSP.BSP2OBJ {
	public class Program {
		public static void Main(string[] args) {
			string bspFile = null;
			string objFile = null;
			bool leaves = false;
			for (int i = 0; i < args.Length; i++) {
				if (args[i].StartsWith("--")) {
					if (args[i] == "--leaves") {
						leaves = true;
					} else {
						throw new Exception("no idea what to do with arg " + args[i]);
					}
				} else if (bspFile == null)
					bspFile = args[i];
				else if (objFile == null)
					bspFile = args[i];
				else
					throw new Exception("no idea what to do with arg " + args[i]);
			}
			byte[] bsp = File.ReadAllBytes(args[0]);
			var file = ECLBSPFile.Load(bsp);
			List<string> obj = new();
			int entIdx = 0;
			int vertIdx = 1;
			foreach (var entity in file.entities) {
				// Console.WriteLine("entity " + entIdx);
				if (entity.model != null) {
					// Console.WriteLine(" model, has " + entity.model.renderables.Count + " renderables");
					int areaIdx = 0;
					foreach (var renderable in entity.model.renderables) {
						List<(ECLBSPFile.Vertex, ECLBSPFile.Vertex, ECLBSPFile.Vertex)> triangles = new();
						// this is here in case any further transform needs to be done at some unspecified future time
						foreach (var tri in renderable.Build())
							triangles.Add(tri);
						WriteOBJObject(obj, entity["classname"] + "_" + entIdx + "a" + areaIdx, renderable.tex, triangles, ref vertIdx);
						areaIdx++;
					}
					if (entity == file.worldspawn) {
						if (leaves) {
							int leafIdx = 0;
							foreach (var leaf in entity.model.viewLeaves) {
								var tmp = Convex3d<bool>.FromPlanes(leaf, false, 0.01d, 65536d, true);
								if (tmp == null)
									continue;
								List<(ECLBSPFile.Vertex, ECLBSPFile.Vertex, ECLBSPFile.Vertex)> leafTris = new();
								Convex2Tris(tmp, leafTris);
								if (leafTris.Count > 0)
									WriteOBJObject(obj, entity["classname"] + "_" + entIdx + "l" + leafIdx, "occluder", leafTris, ref vertIdx);
								leafIdx++;
							}
						}
						var occluderGeo = ECLOccy.IntoOcclusionGeometry(entity.model, 16, 32, 0.05d, 65536d, true);
						List<(ECLBSPFile.Vertex, ECLBSPFile.Vertex, ECLBSPFile.Vertex)> occyTris = new();
						foreach (var geo in occluderGeo)
							Convex2Tris(geo, occyTris);
						WriteOBJObject(obj, "occluderGeo", "occluder", occyTris, ref vertIdx);
					}
				}
				entIdx++;
			}
			File.WriteAllLines(args[1], obj);
		}
		public static void Convex2Tris(Convex3d<bool> tmp, List<(ECLBSPFile.Vertex, ECLBSPFile.Vertex, ECLBSPFile.Vertex)> leafTris) {
			foreach (var face in tmp.faces) {
				ECLBSPFile.Vertex ConvVtx(Vector3d vtx) {
					return new ECLBSPFile.Vertex {
						position = vtx,
						normal = face.plane.normal
					};
				}
				for (int i = 2; i < face.winding.Count; i++) {
					leafTris.Add((
						ConvVtx(face.winding[0]),
						ConvVtx(face.winding[i - 1]),
						ConvVtx(face.winding[i])
					));
				}
			}
		}
		public static void WriteOBJObject(List<string> obj, string objectName, string materialName, List<(ECLBSPFile.Vertex, ECLBSPFile.Vertex, ECLBSPFile.Vertex)> triangles, ref int vertIdx) {
			obj.Add("o " + objectName);
			obj.Add("usemtl " + materialName);
			foreach (var tri in triangles) {
				// loading into Blender at least seems to reverse the triangles
				foreach (var vertex in new ECLBSPFile.Vertex[] { tri.Item3, tri.Item2, tri.Item1 }) {
					obj.Add($"v {vertex.position.x} {vertex.position.y} {vertex.position.z}");
					obj.Add($"vt {vertex.uv.x} {vertex.uv.y}");
					obj.Add($"vn {vertex.normal.x} {vertex.normal.y} {vertex.normal.z}");
				}
				obj.Add($"f {vertIdx}/{vertIdx}/{vertIdx} {vertIdx + 1}/{vertIdx + 1}/{vertIdx + 1} {vertIdx + 2}/{vertIdx + 2}/{vertIdx + 2}");
				vertIdx += 3;
			}
		}
	}
}
