using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using KDCVRCBSP.ECL;

namespace KDCVRCBSP.BSP2OBJ {
	public class Program {
		public static void Main(string[] args) {
			byte[] bsp = File.ReadAllBytes(args[0]);
			var file = ECLBSPFile.Load(bsp);
			List<string> obj = new();
			int entIdx = 0;
			int vertIdx = 1;
			foreach (var entity in file.entities) {
				Console.WriteLine("entity " + entIdx);
				if (entity.model != null) {
					Console.WriteLine(" model, has " + entity.model.areas.Count + " areas");
					int areaIdx = 0;
					foreach (var area in entity.model.areas) {
						obj.Add("o " + entity["classname"] + "_" + entIdx + "a" + areaIdx);
						foreach (var renderable in area) {
							if (renderable is ECLBSPFile.ModelTriangle tri) {
								obj.Add("usemtl " + tri.tex);
								// loading into Blender at least seems to reverse the triangles
								foreach (var vertex in new ECLBSPFile.Vertex[] { tri.c, tri.b, tri.a }) {
									obj.Add($"v {vertex.position.x} {vertex.position.y} {vertex.position.z}");
									obj.Add($"vt {vertex.uv.x} {vertex.uv.y}");
									obj.Add($"vn {vertex.normal.x} {vertex.normal.y} {vertex.normal.z}");
								}
								obj.Add($"f {vertIdx}/{vertIdx}/{vertIdx} {vertIdx + 1}/{vertIdx + 1}/{vertIdx + 1} {vertIdx + 2}/{vertIdx + 2}/{vertIdx + 2}");
								vertIdx += 3;
							}
						}
						areaIdx++;
					}
				}
				entIdx++;
			}
			File.WriteAllLines(args[1], obj);
		}
	}
}
