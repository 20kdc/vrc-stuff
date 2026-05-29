using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using KDCVRCBSP.ECL;

namespace KDCVRCBSP.CMF {
	public class CMFFile {
		/// for binary equivalence test reasons, we write the same WAD path that was used by NMS.
		public const string ConsistentWADPath = "\\projects\\digipen\\nuclear monkey software\\narbacular drop\\cvsprojects\\world craft\\wads\\narbaculardrop.wad";

		public List<string> materials = new();
		public List<Entity> entities = new();

		public int EnsureMaterial(string str) {
			int materialIndex = materials.IndexOf(str);
			if (materialIndex == -1) {
				materialIndex = materials.Count;
				materials.Add(str);
			}
			return materialIndex;
		}

		public void EmitStr(MemoryStream ms, string str) {
			ms.Write(new UTF8Encoding(false).GetBytes(str));
			ms.WriteByte(0);
		}

		public byte[] Emit(bool ver3) {
			MemoryStream ms = new MemoryStream();
			ms.WriteByte((byte) 'C');
			ms.WriteByte((byte) 'M');
			ms.WriteByte((byte) 'F');
			if (ver3) {
				ms.WriteByte((byte) 3);
				// dummy 'preview image'
				// this is here for binary equivalence checks
				ms.Write(BitConverter.GetBytes((int) 128));
				ms.Write(BitConverter.GetBytes((int) 128));
				byte[] dummy = BitConverter.GetBytes((uint) 0xFF808080);
				for (int i = 0; i < 128 * 128; i++)
					ms.Write(dummy);
			} else {
				ms.WriteByte((byte) 2);
			}
			// continue
			ms.Write(BitConverter.GetBytes((int) 1));
			ms.Write(BitConverter.GetBytes((int) entities.Count));
			ms.Write(BitConverter.GetBytes((int) materials.Count));
			// wadPath is just copied from wad key of worldspawn
			// failing this we use the 'digipen wadpath'
			string wadPath = ConsistentWADPath;
			if (entities.Count > 0)
				foreach (var pair in entities[0].pairs)
					if (pair.Item1 == "wad")
						wadPath = pair.Item2;
			EmitStr(ms, wadPath);
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
