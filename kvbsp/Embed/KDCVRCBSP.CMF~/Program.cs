using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using KDCVRCBSP.ECL;

/// 'Simple and silly' test program to mess with BSP compilation techniques.
/// By default, this acts as an open-source 'good enough' replacement for Narbacular Drop's 'csg.exe'.
/// Notably, it doesn't have the weird random parsing issues.
namespace KDCVRCBSP.CMF {
	public class Program {
		public static void Main(string[] args) {
			string input = "";
			string output = "";
			string texdir = "video/";
			bool switches = true;
			bool isWaitingForTexdir = false;
			bool isWaitingForExtract = false;
			Dictionary<string, Vector2d> textures = new();
			foreach (string s in args) {
				if (isWaitingForTexdir) {
					texdir = s;
					isWaitingForTexdir = false;
				} if (isWaitingForExtract) {
					// extract File.ore
					byte[] ore = File.ReadAllBytes(s + "File.ore");
					ExtractORE(ore, 0, s);
					return;
				} else if (switches && s.StartsWith("-")) {
					if (s == "--") {
						switches = false;
					} else if (s == "--texdir") {
						isWaitingForTexdir = true;
					} else if (s == "--extract") {
						isWaitingForExtract = true;
					} else {
						throw new Exception("unknown switch");
					}
				} else if (input == "") {
					input = s;
				} else if (output == "") {
					output = s;
				}
			}
			if (input == "")
				throw new Exception("Input filename needed");
			if (output == "") {
				if (!input.EndsWith(".map"))
					throw new Exception("If omitting output filename, need input to have '.map' at end of name");
				output = input.Substring(0, input.Length - 3) + "cmf";
			}
			List<EntityParsed> parsedEntities = MapParsing.Parse(File.ReadAllText(input));
			CMFFile cmf = new();
			foreach (EntityParsed ent in parsedEntities) {
				CMFFile.Entity cmfEnt = new();
				// because hammer duplicates classname
				bool didSetClassname = false;
				foreach (var pair in ent.pairs) {
					if (pair.Item1 == "classname" && !didSetClassname) {
						cmfEnt.classname = pair.Item2;
						didSetClassname = true;
						// first classname is ignored for pairs
						// my guess is that real ND csg.exe requires classname to be first key
						continue;
					} else if (pair.Item1 == "mapversion") {
						// csg.exe absorbs this too
						continue;
					} else if (pair.Item1 == "wad") {
						cmfEnt.pairs.Add(("wad", CMFFile.ConsistentWADPath));
						continue;
					}
					cmfEnt.pairs.Add(pair);
				}
				foreach (var brush in ent.brushes) {
					var planes = EntityParsed.BrushPlanes(brush);
					for (int face = 0; face < brush.Count; face++) {
						var faceDat = brush[face];
						var facePlane = planes[face];
						var winding = GeomUtil.GenInitialWinding(facePlane, 65536d);
						for (int cutter = 0; cutter < brush.Count; cutter++) {
							if (cutter == face)
								continue;
							planes[cutter].CutWinding(winding, 0.0078125d);
						}
						if (winding.Count < 3)
							continue;
						CMFFile.Polygon poly = new();
						poly.materialIndex = cmf.materials.IndexOf(faceDat.texture);
						var texSize = GetTexSize(faceDat.texture, textures, texdir);
						if (poly.materialIndex == -1) {
							poly.materialIndex = cmf.materials.Count;
							cmf.materials.Add(faceDat.texture);
						}
						// convert into CMF coordinate system
						poly.plane = new Plane3d(new Vector3d(facePlane.normal.x, facePlane.normal.z, facePlane.normal.y), facePlane.distance);
						foreach (Vector3d vec in winding) {
							// convert into CMF coordinate system
							Vector3d vecConv = new Vector3d(vec.x, vec.z, vec.y);
							poly.vertices.Add((vecConv, faceDat.MapUV(vec) / texSize));
						}
						cmfEnt.polygons.Add(poly);
					}
				}
				cmf.entities.Add(cmfEnt);
			}
			File.WriteAllBytes(output, cmf.Emit());
		}
		public static Vector2d GetTexSize(string tex, Dictionary<string, Vector2d> cache, string texdir) {
			if (cache.ContainsKey(tex)) {
				return cache[tex];
			}
			// File.ore normalizes to lower-case.
			// It's evident from how Levels is referred to as such that this normalization is also used in the engine.
			string texfile = texdir + (tex.ToLowerInvariant()) + ".bmp";
			byte[] bmp = File.ReadAllBytes(texfile);
			int w = BitConverter.ToInt32(bmp, 0x12);
			int h = BitConverter.ToInt32(bmp, 0x16);
			Vector2d texSize = new Vector2d(w, h);
			cache[tex] = texSize;
			return texSize;
		}
		public static string ORECStr(byte[] data, ref int at) {
			int start = at;
			while (true) {
				byte v = data[at++];
				if (v == 0)
					break;
			}
			return UTF8Encoding.Latin1.GetString(data, start, at - (start + 1));
		}
		public static void ExtractORE(byte[] data, int at, string pfx) {
			int cur = at;
			int filesAt = at + BitConverter.ToInt32(data, cur);
			cur += 4;
			int dirCount = BitConverter.ToInt32(data, cur);
			cur += 4;
			for (int i = 0; i < dirCount; i++) {
				string dirName = ORECStr(data, ref cur);
				int dirOfs = at + BitConverter.ToInt32(data, cur);
				cur += 4;
				Directory.CreateDirectory(pfx + dirName);
				ExtractORE(data, dirOfs, pfx + dirName + "/");
			}
			cur = filesAt;
			int fileCount = BitConverter.ToInt32(data, cur);
			cur += 4;
			for (int i = 0; i < fileCount; i++) {
				string fileName = ORECStr(data, ref cur);
				int fileOfs = filesAt + BitConverter.ToInt32(data, cur);
				cur += 4;
				int fileLen = BitConverter.ToInt32(data, cur);
				cur += 4;
				byte[] fileData = new byte[fileLen];
				Console.WriteLine("extract: " + pfx + fileName);
				Buffer.BlockCopy(data, fileOfs, fileData, 0, fileLen);
				File.WriteAllBytes(pfx + fileName, fileData);
			}
		}
	}
}
