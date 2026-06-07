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

		/// material class
		public sealed class Material: IBSPMaterial {
			public string texture;
			public Vector2d size;
			public BSPSurfaceFlags surfaceFlags;
			public BSPSurfaceFlags transFlags;
			public BSPSurfaceFlags SurfaceFlags => surfaceFlags;
			public BSPSurfaceFlags TransFlags => transFlags;
			public override string ToString() => texture;
		}

		public sealed class Diag : IBSPDiagnostics {
			public string outPfx = "/KDCBSP_CMF_BUG";

			public void Info(string text) {
				Console.WriteLine("INFO: " + text);
			}

			public void Warning(string text) {
				Console.WriteLine("WARN: " + text);
			}

			public bool DebugEnabled => true;

			public BSPCompileFlags CompileFlags { get; set; }

			public void WriteDiagFileDebug(string filename, Func<List<string>> text) {
				WriteDiagFileInfo(filename, text);
			}

			public void WriteDiagFileInfo(string filename, Func<List<string>> text) {
				File.WriteAllLines(outPfx + filename, text());
			}

			public void WriteDiagFileWarning(string filename, Func<List<string>> text) {
				WriteDiagFileInfo(filename, text);
			}
		}

		public static void Main(string[] args) {
			string input = "";
			string output = "";
			string texdir = "video/";
			bool switches = true;
			bool isWaitingForTexdir = false;
			bool isWaitingForExtract = false;
			bool digipenWad = false;
			bool minify = false;
			bool chop = false;
			// We enable tbsim by default because it will have no effect on campaign maps.
			bool tbsim = true;
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
					} else if (s == "--digipen-wad") {
						digipenWad = true;
					} else if (s == "--minify") {
						minify = true;
					} else if (s == "--chop") {
						chop = true;
					} else if (s == "--no-tbsim") {
						tbsim = false;
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
			Dictionary<string, Material> textures = new();
			textures["noclip"] = new Material {
				texture = "noclip",
				size = (1, 1),
				surfaceFlags = BSPSurfaceFlags.NoChopThis | BSPSurfaceFlags.DeleteAreaColliderFace | BSPSurfaceFlags.DeleteAreaRenderFace,
				transFlags = 0,
			};
			var parsedEntities = MapParser.Parse<Material>(File.ReadAllText(input), (name) => GetTexSize(name, textures, texdir));
			// preprocessing: UV scaling
			foreach (var ent in parsedEntities) {
				foreach (var brush in ent.brushes) {
					foreach (var face in brush) {
						face.texUV.texOffset /= face.texture.size;
						face.texUV.texSAxis /= face.texture.size.x;
						face.texUV.texTAxis /= face.texture.size.y;
					}
				}
			}
			// preprocessing: TrenchBroom simulation
			if (tbsim)
				TrenchBroom.FullSimulateExport(parsedEntities);
			var worldspawn = EntityParsed<Material>.EnsureWorldspawn(parsedEntities);
			CMFFile cmf = new();
			if (worldspawn.pairs.GetBool("_kvbsp", false)) {
				// 'modern pipeline'
				var diag = new Diag {
					outPfx = output
				};
				diag.CompileFlags = 0;
				if (!chop)
					diag.CompileFlags |= BSPCompileFlags.NoChop;
				var map = BSPHighLevel.Act1_MapIntoGeo2(parsedEntities, diag);
				BSPHighLevel.Act2_CompileAll(map, (entity) => {
					return true;
				}, diag);
				BSPHighLevel.Act3_Postprocess(map, diag);

				var worldspawnCMFEnt = new CMFFile.Entity();
				cmf.entities.Add(worldspawnCMFEnt);
				worldspawnCMFEnt.classname = "worldspawn";
				foreach (var pair in map.worldspawn.pairs)
					worldspawnCMFEnt.pairs.Add(pair);

				// worldspawn geom.
				// for some bizzare reason, the ordering of entities REALLY matters to a scary degree
				int worldLightSlice = worldspawn.pairs.GetInt("_lightslice", 0);
				foreach (var area in map.worldspawn.areas) {
					foreach (var face in area.renderFaces) {
						CMFFile.Entity thisWall = new();
						thisWall.classname = "collidable_geometry";
						thisWall.pairs.Add(("sfx_type", "" + worldspawn.pairs.GetInt("_type:" + face.material.texture, 0)));
						ConvertFace(thisWall.polygons, cmf, face);
						// worldLightSlice
						if (thisWall.polygons.Count == 0)
							continue;
						cmf.entities.Add(thisWall);
					}
				}

				// setup brush entities
				foreach (var ent in map.brushEntities) {
					var classname = ent.pairs["classname"];
					CMFFile.Entity cmfEnt = new();
					cmfEnt.classname = classname;
					foreach (var pair in ent.pairs)
						cmfEnt.pairs.Add(pair);
					cmf.entities.Add(cmfEnt);

					// geometry
					foreach (var area in ent.areas)
						foreach (var face in area.renderFaces)
							ConvertFace(cmfEnt.polygons, cmf, face);
					// , ent.pairs.GetInt("_lightslice", 0)
				}

				// setup point entities
				foreach (var ent in map.pointEntities) {
					CMFFile.Entity cmfEnt = new();
					cmfEnt.classname = ent["classname"];
					foreach (var pair in ent)
						cmfEnt.pairs.Add(pair);
					cmf.entities.Add(cmfEnt);
				}
			} else {
				// 'traditional' CMF pipeline
				foreach (var ent in parsedEntities) {
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
							if (minify) {
								// minify writes an empty wad key
								// the game definitely doesn't care about the value (it's nonsense)
								// but it might check the pair, and the one in the CMF header needs to at least be an empty string
								cmfEnt.pairs.Add(("wad", ""));
								continue;
							} else if (digipenWad) {
								cmfEnt.pairs.Add(("wad", CMFFile.ConsistentWADPath));
								continue;
							}
						}
						cmfEnt.pairs.Add(pair);
					}

					List<Convex3d<EntityParsed<Material>.BrushSide>> brushesConvexes = new();
					// We create a new Geo2Context for each entity.
					// Unless you're trying to write a 'literal' BSP file,
					//  you should be doing this to save on plane lookups.
					Geo2Context g2 = new(new());
					foreach (var brush in ent.brushes) {
						var cvx = Convex3d<EntityParsed<Material>.BrushSide>.FromBrush(g2, brush, (idx, v) => v);
						if (cvx != null)
							brushesConvexes.Add(cvx);
					}

					// Collate all faces.
					var faces = MaybeChop(brushesConvexes, chop);

					// _kvbsp_partition enables doing proper partitioning and thus dead brush elimination.
					if (ent.pairs.GetBool("_kvbsp_partition", false))
						Partition(output, g2, parsedEntities, faces);

					// Continue...
					foreach (var face in faces) {
						List<(Vector3d, Vector2d)> polygon = new();
						foreach (var pt in face.winding)
							polygon.Add((pt, face.data.MapUV(pt)));
						for (int i = 2; i < face.winding.Count; i++) {
							ConvertFace(cmfEnt.polygons, cmf, new Geo2RenderFace<Material> {
								material = face.data.texture,
								plane = g2.FromPlaneIndex(face.planeIndex),
								a = polygon[0],
								b = polygon[i - 1],
								c = polygon[i]
							});
							// , ent.pairs.GetInt("_lightslice", 0)
						}
					}
					cmf.entities.Add(cmfEnt);
				}
			}
			File.WriteAllBytes(output, cmf.Emit(!minify));
		}

		public static List<Convex3d<EntityParsed<Material>.BrushSide>.Face> MaybeChop(List<Convex3d<EntityParsed<Material>.BrushSide>> brushesConvexes, bool chop) {
			List<Convex3d<EntityParsed<Material>.BrushSide>.Face> faces = new();
			for (int cvxIdx = 0; cvxIdx < brushesConvexes.Count; cvxIdx++) {
				var cvx = brushesConvexes[cvxIdx];
				IReadOnlyList<Convex3d<EntityParsed<Material>.BrushSide>.Face> brushFaces = cvx.faces;
				if (chop)
					brushFaces = cvx.ChopFaces(brushesConvexes, (f) => f.data.texture.SurfaceFlags);
				foreach (var f in brushFaces)
					faces.Add(f);
			}
			return faces;
		}

		public static void Partition(string output, Geo2Context g2, List<EntityParsed<Material>> parsedEntities, List<Convex3d<EntityParsed<Material>.BrushSide>.Face> faces) {
			Vector3d partitionVec = Vector3d.Zero;
			foreach (var subEnt in parsedEntities) {
				if (subEnt.pairs["classname"] == "player_respawn") {
					partitionVec = subEnt.pairs.GetVector3d("origin", Vector3d.Zero);
					break;
				}
			}
			Console.WriteLine("Presorting face list...");
			BSPNode<EntityParsed<Material>.BrushSide>.PresortFaceList(faces);
			Console.WriteLine("Building tree (" + faces.Count + " splitting faces...)");
			var tree = BSPNode<EntityParsed<Material>.BrushSide>.Build(g2, faces, Array.Empty<Convex3d<EntityParsed<Material>.BrushSide>.Face>(), Array.Empty<int>(), (_) => true, null);
			if (tree == null) {
				Console.WriteLine("All leaves were solid?");
			} else {
				List<BSPLeaf<EntityParsed<Material>.BrushSide>> leaves = new();
				tree.AddLeaves(leaves);
				Console.WriteLine("Portalizing (" + leaves.Count + " leaves)...");
				BSPNode<EntityParsed<Material>.BrushSide>.Portalize(leaves);
				File.WriteAllLines(output + ".leaves.obj", BSPNode<EntityParsed<Material>.BrushSide>.MakeLeafOBJ(leaves));
				File.WriteAllLines(output + ".prt", BSPNode<EntityParsed<Material>.BrushSide>.MakePRT(leaves));
				var startLeaf = tree.Find(g2, partitionVec);
				// alright, find what we'll let live
				List<BSPLeaf<EntityParsed<Material>.BrushSide>> allSurvivingLeaves = new();
				startLeaf.Explore(allSurvivingLeaves, new(), (_) => true);
				faces.Clear();
				HashSet<Convex3d<EntityParsed<Material>.BrushSide>.Face> seenFaces = new();
				foreach (var leaf in allSurvivingLeaves)
					foreach (var face in leaf.faces)
						if (seenFaces.Add(face))
							faces.Add(face);
			}
		}

		public static void ConvertFace(List<CMFFile.Polygon> polygons, CMFFile cmf, Geo2RenderFace<Material> face) {
			int materialIndex = cmf.EnsureMaterial(face.material.texture);
			// convert into CMF coordinate system
			var cmfPlane = new Plane3d(new Vector3d(face.plane.normal.x, face.plane.normal.z, face.plane.normal.y), -face.plane.distance);
			var poly = new CMFFile.Polygon {
				materialIndex = materialIndex,
				// convert into CMF coordinate system
				plane = cmfPlane
			};
			foreach (var vecuv in new (Vector3d, Vector2d)[] {face.a, face.b, face.c}) {
				// also convert into CMF coordinate system
				Vector3d vecConv = new(vecuv.Item1.x, vecuv.Item1.z, vecuv.Item1.y);
				poly.vertices.Add((vecConv, vecuv.Item2));
			}
			polygons.Add(poly);
		}

		public static void LightSlicerAbandoned(List<List<Vector3d>> windings, AABB3d bounds, Plane3d facePlane, int lightSlice, double epsilon) {
			if (lightSlice != 0) {
				// light slicer active; pick primary axis
				int primaryAxis = facePlane.normal.PrimaryAxis;
				if (primaryAxis == 0) {
					LightSlicerCutWindings(windings, new Vector3d(0, 1, 0), bounds, lightSlice, epsilon);
					LightSlicerCutWindings(windings, new Vector3d(0, 0, 1), bounds, lightSlice, epsilon);
				} else if (primaryAxis == 1) {
					LightSlicerCutWindings(windings, new Vector3d(1, 0, 0), bounds, lightSlice, epsilon);
					LightSlicerCutWindings(windings, new Vector3d(0, 0, 1), bounds, lightSlice, epsilon);
				} else {
					LightSlicerCutWindings(windings, new Vector3d(1, 0, 0), bounds, lightSlice, epsilon);
					LightSlicerCutWindings(windings, new Vector3d(0, 1, 0), bounds, lightSlice, epsilon);
				}
			}
		}

		// This is used to provide good results with Narbacular Drop's vertex lighting system.
		// The plane is assumed to be axis-aligned and positive.
		public static void LightSlicerCutWindings(List<List<Vector3d>> windings, Vector3d planeNormal, AABB3d bounds, int lightSlice, double epsilon) {
			Plane3d plane = new Plane3d(planeNormal, 0);
			int min = (int) Math.Floor(plane.SignedDistance(bounds.min) - 1);
			int max = (int) Math.Ceiling(plane.SignedDistance(bounds.max) + 1);
			int minDiv = min / lightSlice;
			int maxDiv = max / lightSlice;
			// Console.WriteLine("lightSlicer dbg " + minDiv + " " + maxDiv);
			// The very small half-epsilon offset prevents falling through the floor.
			for (int i = minDiv; i <= maxDiv; i++)
				LightSlicerCutWindings(windings, new Plane3d(planeNormal, (i * lightSlice) + (epsilon / 2)), epsilon);
		}

		// This is used to provide good results with Narbacular Drop's vertex lighting system.
		// The plane is assumed to be axis-aligned and positive.
		public static void LightSlicerCutWindings(List<List<Vector3d>> windings, Plane3d plane, double epsilon) {
			int i = 0;
			while (i < windings.Count) {
				var winding = windings[i];
				var posWinding = new List<Vector3d>();
				plane.CutWinding(winding, posWinding, epsilon);
				if (winding.Count < 3) {
					windings.RemoveAt(i);
				} else {
					i++;
				}
				if (posWinding.Count >= 3) {
					windings.Insert(i, posWinding);
					i++;
				}
			}
		}

		public static Material GetTexSize(string tex, Dictionary<string, Material> cache, string texdir) {
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
			BSPSurfaceFlags surfaceFlags = 0;
			if (tex.Equals("aaatrigger", StringComparison.InvariantCultureIgnoreCase))
				surfaceFlags = BSPSurfaceFlags.NoChopThis | BSPSurfaceFlags.NoChopOthers | BSPSurfaceFlags.NoCreateTJunction | BSPSurfaceFlags.NoFixTJunction;
			var ktl = new Material {
				texture = tex,
				size = texSize,
				surfaceFlags = surfaceFlags,
				transFlags = 0
			};
			cache[tex] = ktl;
			return ktl;
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
