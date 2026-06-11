using System.Text;
using System.Collections.Generic;
using System;

namespace KDCVRCBSP.ECL {
	/// Q1/HL loader.
	/// This exists essentially as a joke, because the lack of a brush structure and poor filename support makes it essentially infinitely inferior to the Q2 format for real use.
	public static class ECLQHLLoader {
		// CONTENTS bits aren't bitflags like they are in post-Q1 engines.
		public const int CONTENTS_SOLID = -2;

		public static ECLBSPFile Load(byte[] bspRaw) {
			var bsp = new ECLLoadCom.LumpTable {
				file = new(bspRaw),
				lumpHeaderEntrySize = 8,
				lumpHeaderOfsOfs = 4,
				lumpHeaderLenOfs = 8
			};

			// lumps
			var lumpEntities = bsp[0];
			var lumpPlanes = bsp[1];
			var lumpMiptex = bsp[2];
			var lumpVertexes = bsp[3];
			var lumpNodes = bsp[5];
			var lumpTexInfos = bsp[6];
			var lumpFaces = bsp[7];
			var lumpLeaves = bsp[10];
			var lumpEdges = bsp[12];
			var lumpSurfEdges = bsp[13];
			var lumpModels = bsp[14];

			Plane3d[] planes = ECLLoadCom.HandlePlanesLump(lumpPlanes, false);
			Vector3d[] vertexes = ECLLoadCom.HandleVertexLump(lumpVertexes);

			var encoding = new UTF8Encoding(false);

			(string, BrushUV)[] texInfos;
			foreach ((int idx, var pos) in lumpTexInfos.StructArray(40, out texInfos)) {
				int texId = pos.GetS32(32);
				int strofs = lumpMiptex.GetS32(4 + (texId * 4));
				int strlen = 0;
				while (strlen < 16) {
					if (lumpMiptex[strofs + strlen] == 0)
						break;
					strlen++;
				}
				string name = encoding.GetString(lumpMiptex.data, lumpMiptex.ofs + strofs, strlen);
				texInfos[idx] = (name, new BrushUV {
					texSAxis = new Vector3d(
						pos.GetF32(0),
						pos.GetF32(4),
						pos.GetF32(8)
					),
					texTAxis = new Vector3d(
						pos.GetF32(16),
						pos.GetF32(20),
						pos.GetF32(24)
					),
					texOffset = new Vector2d(
						pos.GetF32(12),
						pos.GetF32(28)
					),
					rotation = 0,
				});
			}

			/// Gets TexInfo or a fake one.
			/// This is useful because nodraw faces don't necessarily have valid TexInfos.
			/// This comes up in collision processing.
			(string, BrushUV) GetTexInfoOrFallback(int i) {
				if (i >= 0 && i < texInfos.Length)
					return texInfos[i];
				return ("fallback", new BrushUV {
					texSAxis = (1, 0, 0),
					texTAxis = (0, 1, 0)
				});
			}

			// This is done in an inner function to ensure these are duplicated.
			// This reduces the chance of issues when translation occurs.
			void AddFaceToModel(ECLBSPFile.Model model, int face, Dictionary<int, Dictionary<string, ECLBSPFile.ModelTriMesh>> areaTable) {
				var pos = lumpFaces.GetStruct(face, 20);
				int plane, side, firstEdge, numEdges, texInfo;
				plane = pos.GetU16(0);
				side = pos.GetU16(2);
				firstEdge = pos.GetS32(4);
				numEdges = pos.GetU16(8);
				texInfo = pos.GetU16(10);
				Plane3d planeCorrected = planes[plane];
				if (side != 0)
					planeCorrected = planeCorrected.Flipped;
				var texInfoVal = GetTexInfoOrFallback(texInfo);
				ECLMesh.Vertex[] winding = new ECLMesh.Vertex[numEdges];
				for (int i = 0; i < numEdges; i++) {
					// this is basically the same, right?
					int sebVal = lumpSurfEdges.GetS32((firstEdge + i) * 4); // surfedge
					int edgeIdx = sebVal < 0 ? -sebVal : sebVal;
					int edgeVtx = sebVal < 0 ? 1 : 0;
					// edges[edgeIdx][edgeVtx]
					int totalVtx = (edgeIdx * 2) + edgeVtx;
					int vertex;
					vertex = lumpEdges.GetU16(totalVtx * 2);
					var vtx = vertexes[vertex];
					winding[i] = new ECLMesh.Vertex {
						position = vtx,
						uv = texInfoVal.Item2.MapUV(vtx),
						normal = planeCorrected.normal,
						colourR = 255,
						colourG = 255,
						colourB = 255,
						colourA = 255
					};
				}
				model.AddPolygon(texInfoVal.Item1, winding, 0, areaTable);
			}

			// Despite lump order, models *must* be processed just before entities.
			// This is because we have to assemble lots of details out of everything else in the file.
			// Then, entities need to have models ready.

			/// Models. Entities point to these (or implicitly in the case of model 0, aka worldspawn).
			ECLBSPFile.Model[] models;
			foreach ((int idx, var pos) in lumpModels.StructArray(64, out models)) {
				int headNode = pos.GetS32(36);
				int faceStart = pos.GetS32(56);
				int faceCount = pos.GetS32(60);
				var model = new ECLBSPFile.Model {
					bounds = new AABB3d(
						(
							pos.GetF32(0),
							pos.GetF32(4),
							pos.GetF32(8)
						),
						(
							pos.GetF32(12),
							pos.GetF32(16),
							pos.GetF32(20)
						)
					),
					//origin = GetPosition(bsp, pos + 24, worldScale),
				};
				Dictionary<int, Dictionary<string, ECLBSPFile.ModelTriMesh>> areaTable = new();
				for (int i = 0; i < faceCount; i++)
					AddFaceToModel(model, faceStart + i, areaTable);
				Plane3d[] axialPlanes = ECLLoadCom.FakeBrushesAxialPlanes(model.bounds);
				void CollectLeaves(int node, Plane3d[] splitChain) {
					if (node < 0) {
						// dleaf_t 4 + 4 + (3 * 2) + (3 * 2) + 2 + 2 + 4
						var leafOfs = lumpLeaves.GetStruct(-(node + 1), 28);
						int contents = leafOfs.GetS32(0);
						if (contents != CONTENTS_SOLID) {
							model.viewLeaves.Add(splitChain);
						} else {
							Convex3d<bool> fakeConvex = Convex3d<bool>.FromPlanes(splitChain, false, ECLLoadCom.FakeBrushesEpsilon, ECLLoadCom.FakeBrushesWindingSize);
							if (fakeConvex != null) {
								ECLBSPFile.BrushSide[] sides = new ECLBSPFile.BrushSide[fakeConvex.faces.Count];
								for (int i = 0; i < sides.Length; i++)
									sides[i] = new ECLBSPFile.BrushSide {
										plane = fakeConvex.faces[i].plane,
										tex = "common/clip",
										texUV = new BrushUV {
											texSAxis = (1, 0, 0),
											texTAxis = (0, 1, 0),
										}
									};
								model.brushes.Add(new ECLBSPFile.Brush {
									illusionary = false,
									occyViewpoint = false,
									sides = sides
								});
							}
						}
					} else {
						// dnode_t 4 + (2 * 2) + (3 * 2) + (3 * 2) + 2 + 2
						ECLLoadCom.View nodeOfs = lumpNodes.GetStruct(node, 24);
						int planeIndex = nodeOfs.GetS32(0);
						//Console.WriteLine(planeIndex);
						var plane = planes[planeIndex];
						int childA = nodeOfs.GetS16(4);
						int childB = nodeOfs.GetS16(6);
						CollectLeaves(childA, ECLLoadCom.AppendPlane(splitChain, plane.Flipped));
						CollectLeaves(childB, ECLLoadCom.AppendPlane(splitChain, plane));
					}
				}
				CollectLeaves(headNode, axialPlanes);
				models[idx] = model;
			}

			return ECLLoadCom.ParseQuakeEntities(lumpEntities, models);
		}
	}
}
