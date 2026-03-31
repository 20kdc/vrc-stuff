using System;
using System.IO;
using System.Text;
using System.Collections.Generic;
using UnityEngine;

namespace KDCVRCBSP {
	/**
	 * Loads a Quake 2 BSP file.
	 * This contains all the binary format details, and 'skips over' certain parts of the format.
	 */
	public static class KDCBSPQ2Loader {
		public static KDCBSPIntermediate Load(byte[] bsp, float worldScale, bool qbism) {
			KDCBSPIntermediate res = new();

			int entsOffset = BitConverter.ToInt32(bsp, 8);
			int entsLength = BitConverter.ToInt32(bsp, 12);
			// We bet on treating NUL as whitespace.
			string entitiesLump = new UTF8Encoding(false).GetString(bsp, entsOffset, entsLength);
			res.ParseEntities(entitiesLump, worldScale);

			Plane[] planes;
			foreach ((int idx, int pos) in StructArray(bsp, 1, 20, out planes)) {
				float nX = BitConverter.ToSingle(bsp, pos);
				float nY = BitConverter.ToSingle(bsp, pos + 4);
				float nZ = BitConverter.ToSingle(bsp, pos + 8);
				float d = BitConverter.ToSingle(bsp, pos + 12);
				planes[idx] = KDCBSPIntermediate.TransformPlane(nX, nY, nZ, d, worldScale);
			}

			Vector3[] vertexes;
			foreach ((int idx, int pos) in StructArray(bsp, 2, 12, out vertexes)) {
				vertexes[idx] = GetPosition(bsp, pos, worldScale);
			}

			foreach ((int idx, int pos) in StructArray(bsp, 5, 76, out res.texInfos)) {
				int strofs = pos + 40;
				int strlen = 0;
				while (strlen < 32) {
					if (bsp[strofs + strlen] == 0)
						break;
					strlen++;
				}
				string name = new System.Text.UTF8Encoding().GetString(bsp, strofs, strlen);
				res.texInfos[idx] = new KDCBSPIntermediate.TexInfo {
					// [TRANSFORM]
					// Note the need to perform axis swapping, world scaling...
					sX = BitConverter.ToSingle(bsp, pos + 0) * worldScale,
					sZ = BitConverter.ToSingle(bsp, pos + 4) * worldScale,
					sY = BitConverter.ToSingle(bsp, pos + 8) * worldScale,
					sO = BitConverter.ToSingle(bsp, pos + 12),
					tX = BitConverter.ToSingle(bsp, pos + 16) * -worldScale,
					tZ = BitConverter.ToSingle(bsp, pos + 20) * -worldScale,
					tY = BitConverter.ToSingle(bsp, pos + 24) * -worldScale,
					tO = BitConverter.ToSingle(bsp, pos + 28) * -1,
					tex = name
				};
			}

			KDCBSPIntermediate.Face[] faces;
			foreach ((int idx, int pos) in StructArray(bsp, 6, qbism ? 28 : 20, out faces)) {
				int firstEdge, numEdges, texInfo;
				if (!qbism) {
					firstEdge = BitConverter.ToInt32(bsp, pos + 4);
					numEdges = (int) BitConverter.ToUInt16(bsp, pos + 8);
					texInfo = (int) BitConverter.ToUInt16(bsp, pos + 10);
				} else {
					firstEdge = BitConverter.ToInt32(bsp, pos + 8);
					numEdges = BitConverter.ToInt32(bsp, pos + 12);
					texInfo = BitConverter.ToInt32(bsp, pos + 16);
				}
				Vector3[] winding = new Vector3[numEdges];
				for (int i = 0; i < numEdges; i++) {
					// has to be mapped using surfedges - see https://github.com/id-Software/Quake-2-Tools/blob/master/bsp/qbsp3/writebsp.c#L213
					int seb = GetStructOfs(bsp, 12, firstEdge + i, 4); // surfedge
					int sebVal = BitConverter.ToInt32(bsp, seb);
					int edgeIdx = sebVal < 0 ? -sebVal : sebVal;
					int edgeVtx = sebVal < 0 ? 1 : 0;
					int vertex;
					if (!qbism) {
						int edgeVtxOfs = GetStructOfs(bsp, 11, edgeIdx, 4) + (edgeVtx * 2); // edges[edgeIdx][edgeVtx]
						vertex = (int) BitConverter.ToUInt16(bsp, edgeVtxOfs);
					} else {
						int edgeVtxOfs = GetStructOfs(bsp, 11, edgeIdx, 8) + (edgeVtx * 4);
						vertex = BitConverter.ToInt32(bsp, edgeVtxOfs);
					}
					winding[i] = vertexes[vertex];
				}
				faces[idx] = new KDCBSPIntermediate.Face {
					texInfo = texInfo,
					winding = winding
				};
			}

			KDCBSPIntermediate.Brush[] brushes;
			foreach ((int idx, int pos) in StructArray(bsp, 14, 12, out brushes)) {
				int firstSide = BitConverter.ToInt32(bsp, pos);
				int numSides = BitConverter.ToInt32(bsp, pos + 4);
				int contents = BitConverter.ToInt32(bsp, pos + 8);
				KDCBSPIntermediate.BrushSide[] brushSides = new KDCBSPIntermediate.BrushSide[numSides];
				for (int j = 0; j < numSides; j++) {
					if (!qbism) {
						int sidePos = GetStructOfs(bsp, 15, j + firstSide, 4);
						brushSides[j] = new KDCBSPIntermediate.BrushSide {
							plane = planes[(int) BitConverter.ToUInt16(bsp, sidePos)],
							texInfo = (int) BitConverter.ToUInt16(bsp, sidePos + 2)
						};
					} else {
						int sidePos = GetStructOfs(bsp, 15, j + firstSide, 8);
						brushSides[j] = new KDCBSPIntermediate.BrushSide {
							plane = planes[BitConverter.ToInt32(bsp, sidePos)],
							texInfo = BitConverter.ToInt32(bsp, sidePos + 4)
						};
					}
				}
				brushes[idx] = new KDCBSPIntermediate.Brush {
					contents = contents,
					sides = brushSides
				};
			}

			// Despite lump order, models *must* be processed last.
			// This is because we have to assemble lots of details out of everything else in the file.

			foreach ((int idx, int pos) in StructArray(bsp, 13, 48, out res.models)) {
				int firstFace = BitConverter.ToInt32(bsp, pos + 40);
				int numFaces = BitConverter.ToInt32(bsp, pos + 44);
				KDCBSPIntermediate.Face[] modelFaces = new KDCBSPIntermediate.Face[numFaces];
				for (int i = 0; i < numFaces; i++)
					modelFaces[i] = faces[firstFace + i];
				int headNode = BitConverter.ToInt32(bsp, pos + 36);
				HashSet<int> brushNumSet = new();
				if (!qbism)
					CollectBrushes(bsp, brushNumSet, headNode);
				else
					CollectBrushesQbism(bsp, brushNumSet, headNode);
				List<int> brushNums = new();
				foreach (int i in brushNumSet)
					brushNums.Add(i);
				brushNums.Sort();
				KDCBSPIntermediate.Brush[] modelBrushes = new KDCBSPIntermediate.Brush[brushNums.Count];
				for (int i = 0; i < modelBrushes.Length; i++)
					modelBrushes[i] = brushes[brushNums[i]];
				res.models[idx] = new KDCBSPIntermediate.Model {
					mins = GetPosition(bsp, pos + 0, worldScale),
					maxs = GetPosition(bsp, pos + 12, worldScale),
					origin = GetPosition(bsp, pos + 24, worldScale),
					faces = modelFaces,
					brushes = modelBrushes
				};
			}

			res.SetupBrushEntityOrigins();

			return res;
		}

		// -- This --
		private static void CollectBrushes(byte[] bsp, HashSet<int> brushNumSet, int node) {
			if (node < 0) {
				// dleaf_t 4 + 2 + 2 + (3 * 2) + (3 * 2) + 2 + 2 + 2 + 2
				int leafOfs = GetStructOfs(bsp, 8, -(node + 1), 28);
				int firstLeafBrush = (int) BitConverter.ToUInt16(bsp, leafOfs + 24);
				int numLeafBrushes = (int) BitConverter.ToUInt16(bsp, leafOfs + 26);
				for (int i = 0; i < numLeafBrushes; i++) {
					int leafBrushOfs = GetStructOfs(bsp, 10, firstLeafBrush + i, 2);
					int leafBrush = (int) BitConverter.ToUInt16(bsp, leafBrushOfs);
					brushNumSet.Add(leafBrush);
				}
			} else {
				// dnode_t 4 + (4 * 2) + (3 * 2) + (3 * 2) + 2 + 2
				int nodeOfs = GetStructOfs(bsp, 4, node, 28);
				int childA = BitConverter.ToInt32(bsp, nodeOfs + 4);
				int childB = BitConverter.ToInt32(bsp, nodeOfs + 8);
				CollectBrushes(bsp, brushNumSet, childA);
				CollectBrushes(bsp, brushNumSet, childB);
			}
		}
		private static void CollectBrushesQbism(byte[] bsp, HashSet<int> brushNumSet, int node) {
			if (node < 0) {
				// dleaf_tx 4 + 4 + 4 + (3 * 4) + (3 * 4) + 4 + 4 + 4 + 4
				int leafOfs = GetStructOfs(bsp, 8, -(node + 1), 52);
				int firstLeafBrush = BitConverter.ToInt32(bsp, leafOfs + 44);
				int numLeafBrushes = BitConverter.ToInt32(bsp, leafOfs + 48);
				for (int i = 0; i < numLeafBrushes; i++) {
					int leafBrushOfs = GetStructOfs(bsp, 10, firstLeafBrush + i, 4);
					int leafBrush = BitConverter.ToInt32(bsp, leafBrushOfs);
					brushNumSet.Add(leafBrush);
				}
			} else {
				// dnode_tx 4 + 8 + (3 * 4) + (3 * 4) + 4 + 4
				int nodeOfs = GetStructOfs(bsp, 4, node, 44);
				int childA = BitConverter.ToInt32(bsp, nodeOfs + 4);
				int childB = BitConverter.ToInt32(bsp, nodeOfs + 8);
				CollectBrushesQbism(bsp, brushNumSet, childA);
				CollectBrushesQbism(bsp, brushNumSet, childB);
			}
		}

		// -- Geometry --
		private static Vector3 GetPosition(byte[] bsp, int pos, float worldScale) {
			float nX = BitConverter.ToSingle(bsp, pos);
			float nY = BitConverter.ToSingle(bsp, pos + 4);
			float nZ = BitConverter.ToSingle(bsp, pos + 8);
			return KDCBSPIntermediate.TransformPosition(nX, nY, nZ, worldScale);
		}

		// -- BSP access core --

		private static int GetStructOfs(byte[] bsp, int lump, int index, int structLen) {
			int offset = BitConverter.ToInt32(bsp, 8 + (lump * 8));
			int length = BitConverter.ToInt32(bsp, 8 + (lump * 8) + 4);
			int relOffset = index * structLen;
			if (relOffset < 0 || relOffset >= length)
				throw new Exception("Attempt to get lump " + lump + " object " + index + " -- this was out of range");
			return offset + relOffset;
		}

		private static (int, int)[] StructArray<T>(byte[] bsp, int lump, int structLen, out T[] target) {
			int offset = BitConverter.ToInt32(bsp, 8 + (lump * 8));
			int length = BitConverter.ToInt32(bsp, 8 + (lump * 8) + 4);
			int count = length / structLen;
			target = new T[count];
			(int, int)[] locations = new (int, int)[count];
			for (int i = 0; i < count; i++)
				locations[i] = (i, offset + (i * structLen));
			return locations;
		}
	}
}
