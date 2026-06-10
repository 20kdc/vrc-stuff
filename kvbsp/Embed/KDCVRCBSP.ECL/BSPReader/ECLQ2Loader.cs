using System;
using System.Text;
using System.Collections.Generic;

namespace KDCVRCBSP.ECL {
	/// Loads a Quake 2 BSP file.
	/// This handles the binary format details, skipping over parts of the format irrelevant to this use.
	public static class ECLQ2Loader {
		public const int CONTENTS_SOLID      = 0x00000001;
		public const int CONTENTS_PLAYERCLIP = 0x00010000;
		public const int CONTENTS_CURRENT_0  = 0x00040000;
		public static ECLBSPFile Load(byte[] bspRaw, bool qbism) {
			var bsp = new ECLLoadCom.LumpTable {
				file = new(bspRaw),
				lumpHeaderEntrySize = 8,
				lumpHeaderOfsOfs = 8,
				lumpHeaderLenOfs = 12
			};

			// lumps
			var lumpEntities = bsp[0];
			var lumpPlanes = bsp[1];
			var lumpVertexes = bsp[2];
			var lumpNodes = bsp[4];
			var lumpTexInfos = bsp[5];
			var lumpFaces = bsp[6];
			var lumpLeaves = bsp[8];
			var lumpLeafFaces = bsp[9];
			var lumpLeafBrushes = bsp[10];
			var lumpEdges = bsp[11];
			var lumpSurfEdges = bsp[12];
			var lumpModels = bsp[13];
			var lumpBrushes = bsp[14];
			var lumpBrushSides = bsp[15];

			Plane3d[] planes = ECLLoadCom.HandlePlanesLump(lumpPlanes, false);
			Vector3d[] vertexes = ECLLoadCom.HandleVertexLump(lumpVertexes);

			var encoding = new UTF8Encoding(false);

			(string, BrushUV)[] texInfos;
			foreach ((int idx, var pos) in lumpTexInfos.StructArray(76, out texInfos)) {
				int strofs = 40;
				int strlen = 0;
				while (strlen < 32) {
					if (pos[strofs + strlen] == 0)
						break;
					strlen++;
				}
				string name = encoding.GetString(pos.data, pos.ofs + strofs, strlen);
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
			void AddFaceToModel(ECLBSPFile.Model model, int area, int face, Dictionary<int, Dictionary<string, ECLBSPFile.ModelTriMesh>> areaTable) {
				var pos = lumpFaces.GetStruct(face, qbism ? 28 : 20);
				int plane, side, firstEdge, numEdges, texInfo;
				if (!qbism) {
					plane = pos.GetU16(0);
					side = pos.GetU16(2);
					firstEdge = pos.GetS32(4);
					numEdges = pos.GetU16(8);
					texInfo = pos.GetU16(10);
				} else {
					plane = pos.GetS32(0);
					side = pos.GetS32(4);
					firstEdge = pos.GetS32(8);
					numEdges = pos.GetS32(12);
					texInfo = pos.GetS32(16);
				}
				Plane3d planeCorrected = planes[plane];
				if (side != 0)
					planeCorrected = planeCorrected.Flipped;
				var texInfoVal = GetTexInfoOrFallback(texInfo);
				ECLBSPFile.Vertex[] winding = new ECLBSPFile.Vertex[numEdges];
				for (int i = 0; i < numEdges; i++) {
					// has to be mapped using surfedges - see https://github.com/id-Software/Quake-2-Tools/blob/master/bsp/qbsp3/writebsp.c#L213
					int sebVal = lumpSurfEdges.GetS32((firstEdge + i) * 4); // surfedge
					int edgeIdx = sebVal < 0 ? -sebVal : sebVal;
					int edgeVtx = sebVal < 0 ? 1 : 0;
					// edges[edgeIdx][edgeVtx]
					int totalVtx = (edgeIdx * 2) + edgeVtx;
					int vertex;
					if (!qbism) {
						vertex = lumpEdges.GetU16(totalVtx * 2);
					} else {
						vertex = lumpEdges.GetS32(totalVtx * 4);
					}
					var vtx = vertexes[vertex];
					winding[i] = new ECLBSPFile.Vertex {
						position = vtx,
						uv = texInfoVal.Item2.MapUV(vtx),
						normal = planeCorrected.normal,
						colourR = 255,
						colourG = 255,
						colourB = 255,
						colourA = 255
					};
				}
				model.AddPolygon(texInfoVal.Item1, winding, area, areaTable);
			}

			ECLBSPFile.Brush[] brushes;
			foreach ((int idx, var pos) in lumpBrushes.StructArray(12, out brushes)) {
				int firstSide = pos.GetS32(0);
				int numSides = pos.GetS32(4);
				int contents = pos.GetS32(8);
				ECLBSPFile.BrushSide[] brushSides = new ECLBSPFile.BrushSide[numSides];
				for (int j = 0; j < numSides; j++) {
					Plane3d plane;
					(string, BrushUV) texInfo;
					if (!qbism) {
						var sidePos = lumpBrushSides.GetStruct(j + firstSide, 4);
						plane = planes[sidePos.GetU16(0)];
						texInfo = GetTexInfoOrFallback(sidePos.GetU16(2));
					} else {
						var sidePos = lumpBrushSides.GetStruct(j + firstSide, 8);
						plane = planes[sidePos.GetS32(0)];
						texInfo = GetTexInfoOrFallback(sidePos.GetS32(4));
					}
					brushSides[j] = new ECLBSPFile.BrushSide {
						plane = plane,
						tex = texInfo.Item1,
						texUV = texInfo.Item2
					};
				}
				// We use this as a 'secret handshake' to implement the 'noclip' brush.
				// Noclip brushes are solid (so block vis), but don't create collision.
				bool hasNoclipContents = (contents & CONTENTS_CURRENT_0) != 0;
				bool hasClipContents = (contents & (CONTENTS_SOLID | CONTENTS_PLAYERCLIP)) != 0;
				brushes[idx] = new ECLBSPFile.Brush {
					illusionary = hasNoclipContents || !hasClipContents,
					occyViewpoint = hasNoclipContents,
					sides = brushSides
				};
			}

			// Despite lump order, models *must* be processed just before entities.
			// This is because we have to assemble lots of details out of everything else in the file.
			// Then, entities need to have models ready.

			/// Models. Entities point to these (or implicitly in the case of model 0, aka worldspawn).
			ECLBSPFile.Model[] models;
			foreach ((int idx, var pos) in lumpModels.StructArray(48, out models)) {
				int headNode = pos.GetS32(36);
				var model = new ECLBSPFile.Model {
					bounds = new AABB3d((
						pos.GetF32(0),
						pos.GetF32(4),
						pos.GetF32(8)
					), (
						pos.GetF32(12),
						pos.GetF32(16),
						pos.GetF32(20)
					)),
					//origin = GetPosition(bsp, pos + 24, worldScale),
				};
				HashSet<int> brushNumSet = new();
				HashSet<int> faceNumSet = new();
				Dictionary<int, Dictionary<string, ECLBSPFile.ModelTriMesh>> areaTable = new();
				void CollectLeaves(int node, Plane3d[] splitChain) {
					// For qbism format check https://github.com/qbism/q2tools-220/blob/3fcf535be656d8ff38a9b95238fc741f3aebbd09/src/qfiles.h#L575
					if (node < 0) {
						int area;
						int contents;
						int firstLeafFace;
						int numLeafFaces;
						int firstLeafBrush;
						int numLeafBrushes;
						if (!qbism) {
							// dleaf_t 4 + 2 + 2 + (3 * 2) + (3 * 2) + 2 + 2 + 2 + 2
							var leafOfs = lumpLeaves.GetStruct(-(node + 1), 28);
							contents = leafOfs.GetS32(0);
							area = leafOfs.GetU16(6);
							firstLeafFace = leafOfs.GetU16(20);
							numLeafFaces = leafOfs.GetU16(22);
							firstLeafBrush = leafOfs.GetU16(24);
							numLeafBrushes = leafOfs.GetU16(26);
						} else {
							// dleaf_tx 4 + 4 + 4 + (3 * 4) + (3 * 4) + 4 + 4 + 4 + 4
							var leafOfs = lumpLeaves.GetStruct(-(node + 1), 52);
							contents = leafOfs.GetS32(0);
							area = leafOfs.GetS32(8);
							firstLeafFace = leafOfs.GetS32(36);
							numLeafFaces = leafOfs.GetS32(40);
							firstLeafBrush = leafOfs.GetS32(44);
							numLeafBrushes = leafOfs.GetS32(48);
						}
						for (int i = 0; i < numLeafFaces; i++) {
							int leafFace;
							if (!qbism) {
								leafFace = lumpLeafFaces.GetU16((firstLeafFace + i) * 2);
							} else {
								leafFace = lumpLeafFaces.GetS32((firstLeafFace + i) * 4);
							}
							if (faceNumSet.Add(leafFace))
								AddFaceToModel(model, area, leafFace, areaTable);
						}
						for (int i = 0; i < numLeafBrushes; i++) {
							int leafBrush;
							if (!qbism) {
								leafBrush = lumpLeafBrushes.GetU16((firstLeafBrush + i) * 2);
							} else {
								leafBrush = lumpLeafBrushes.GetS32((firstLeafBrush + i) * 4);
							}
							if (brushNumSet.Add(leafBrush))
								model.brushes.Add(brushes[leafBrush]);
						}
						if ((contents & CONTENTS_SOLID) == 0)
							model.viewLeaves.Add(splitChain);
					} else {
						// the start of dnode_t and dnode_tx is the same
						ECLLoadCom.View nodeOfs;
						if (!qbism) {
							// dnode_t 4 + (4 * 2) + (3 * 2) + (3 * 2) + 2 + 2
							nodeOfs = lumpNodes.GetStruct(node, 28);
						} else {
							// dnode_tx 4 + 8 + (3 * 4) + (3 * 4) + 4 + 4
							nodeOfs = lumpNodes.GetStruct(node, 44);
						}
						int planeIndex = nodeOfs.GetS32(0);
						//Console.WriteLine(planeIndex);
						var plane = planes[planeIndex];
						int childA = nodeOfs.GetS32(4);
						int childB = nodeOfs.GetS32(8);
						CollectLeaves(childA, ECLLoadCom.AppendPlane(splitChain, plane.Flipped));
						CollectLeaves(childB, ECLLoadCom.AppendPlane(splitChain, plane));
					}
				}
				CollectLeaves(headNode, Array.Empty<Plane3d>());
				models[idx] = model;
			}

			return ECLLoadCom.ParseQuakeEntities(lumpEntities, models);
		}
	}
}
