using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
using KDCVRCBSP.ECL;

namespace KDCVRCBSP.CMF {
	public class CMFFile {
		public List<string> materials = new();
		public List<Entity> entities = new();

		public void EmitStr(MemoryStream ms, string str) {
			ms.Write(new UTF8Encoding(false).GetBytes(str));
			ms.WriteByte(0);
		}

		public byte[] Emit() {
			MemoryStream ms = new MemoryStream();
			ms.WriteByte((byte) 'C');
			ms.WriteByte((byte) 'M');
			ms.WriteByte((byte) 'F');
			ms.WriteByte((byte) 3);
			// nearly empty 'preview image'
			ms.Write(BitConverter.GetBytes((int) 1));
			ms.Write(BitConverter.GetBytes((int) 1));
			ms.Write(BitConverter.GetBytes((uint) 0xFFFF00FF));
			// continue
			ms.Write(BitConverter.GetBytes((int) 1));
			ms.Write(BitConverter.GetBytes((int) entities.Count));
			ms.Write(BitConverter.GetBytes((int) materials.Count));
			EmitStr(ms, "narbaculardrop.wad");
			foreach (string s in materials) {
				EmitStr(ms, s);
			}
			foreach (Entity ent in entities) {
				EmitStr(ms, ent.classname);
				ms.Write(BitConverter.GetBytes((int) ent.pairs.Count));
				foreach (var pair in ent.pairs) {
					EmitStr(ms, pair.Item1);
					EmitStr(ms, pair.Item2);
				}
				ms.Write(BitConverter.GetBytes((int) ent.polygons.Count));
				foreach (Polygon poly in ent.polygons) {
					ms.Write(BitConverter.GetBytes((int) poly.materialIndex));
					ms.Write(BitConverter.GetBytes((double) poly.plane.normal.x));
					ms.Write(BitConverter.GetBytes((double) poly.plane.normal.y));
					ms.Write(BitConverter.GetBytes((double) poly.plane.normal.z));
					ms.Write(BitConverter.GetBytes((double) poly.plane.distance));
					ms.Write(BitConverter.GetBytes((int) poly.vertices.Count));
					foreach (var vtx in poly.vertices) {
						ms.Write(BitConverter.GetBytes((double) vtx.Item1.x));
						ms.Write(BitConverter.GetBytes((double) vtx.Item1.y));
						ms.Write(BitConverter.GetBytes((double) vtx.Item1.z));
						ms.Write(BitConverter.GetBytes((double) vtx.Item2.x));
						ms.Write(BitConverter.GetBytes((double) vtx.Item2.y));
					}
				}
			}
			return ms.ToArray();
		}

		public class Entity {
			public string classname;
			public List<(string, string)> pairs = new();
			public List<Polygon> polygons = new();
		}

		public class Polygon {
			public int materialIndex;
			public Plane3d plane;
			public List<(Vector3d, Vector2d)> vertices = new();
		}
	}
}
