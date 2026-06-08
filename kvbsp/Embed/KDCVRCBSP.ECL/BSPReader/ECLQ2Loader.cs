using System;
using System.IO;
using System.Text;
using System.Collections.Generic;

namespace KDCVRCBSP.ECL {
	/// Loads a Quake 2 BSP file.
	/// This contains all the binary format details, and 'skips over' certain parts of the format.
	public static class ECLQ2Loader {
		public static ECLBSPFile Load(byte[] bsp, bool qbism) {
			ECLBSPFile res = new();

			Plane3d[] planes;
			foreach ((int idx, int pos) in StructArray(bsp, 1, 20, out planes)) {
				float nX = BitConverter.ToSingle(bsp, pos);
				float nY = BitConverter.ToSingle(bsp, pos + 4);
				float nZ = BitConverter.ToSingle(bsp, pos + 8);
				float d = BitConverter.ToSingle(bsp, pos + 12);
				planes[idx] = new Plane3d((nX, nY, nZ), d);
			}

			Vector3d[] vertexes;
			foreach ((int idx, int pos) in StructArray(bsp, 2, 12, out vertexes)) {
				vertexes[idx] = GetPosition(bsp, pos);
			}

			var encoding = new UTF8Encoding(false);

			(string, BrushUV)[] texInfos;
			foreach ((int idx, int pos) in StructArray(bsp, 5, 76, out texInfos)) {
				int strofs = pos + 40;
				int strlen = 0;
				while (strlen < 32) {
					if (bsp[strofs + strlen] == 0)
						break;
					strlen++;
				}
				string name = encoding.GetString(bsp, strofs, strlen);
				texInfos[idx] = (name, new BrushUV {
					texSAxis = new Vector3d(
						BitConverter.ToSingle(bsp, pos + 0),
						BitConverter.ToSingle(bsp, pos + 4),
						BitConverter.ToSingle(bsp, pos + 8)
					),
					texTAxis = new Vector3d(
						BitConverter.ToSingle(bsp, pos + 16),
						BitConverter.ToSingle(bsp, pos + 20),
						BitConverter.ToSingle(bsp, pos + 24)
					),
					texOffset = new Vector2d(
						BitConverter.ToSingle(bsp, pos + 12),
						BitConverter.ToSingle(bsp, pos + 28)
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
			void AddFaceToModel(ECLBSPFile.Model model, int area, int face, Dictionary<int, List<ECLBSPFile.ModelRenderable>> areaTable) {
				int pos = GetStructOfs(bsp, 6, face, qbism ? 28 : 20);
				int plane, side, firstEdge, numEdges, texInfo;
				if (!qbism) {
					plane = BitConverter.ToUInt16(bsp, pos);
					side = BitConverter.ToUInt16(bsp, pos + 2);
					firstEdge = BitConverter.ToInt32(bsp, pos + 4);
					numEdges = BitConverter.ToUInt16(bsp, pos + 8);
					texInfo = BitConverter.ToUInt16(bsp, pos + 10);
				} else {
					plane = BitConverter.ToInt32(bsp, pos);
					side = BitConverter.ToInt32(bsp, pos + 4);
					firstEdge = BitConverter.ToInt32(bsp, pos + 8);
					numEdges = BitConverter.ToInt32(bsp, pos + 12);
					texInfo = BitConverter.ToInt32(bsp, pos + 16);
				}
				Plane3d planeCorrected = planes[plane];
				if (side != 0)
					planeCorrected = planeCorrected.Flipped;
				var texInfoVal = GetTexInfoOrFallback(texInfo);
				ECLBSPFile.Vertex[] winding = new ECLBSPFile.Vertex[numEdges];
				for (int i = 0; i < numEdges; i++) {
					// has to be mapped using surfedges - see https://github.com/id-Software/Quake-2-Tools/blob/master/bsp/qbsp3/writebsp.c#L213
					int seb = GetStructOfs(bsp, 12, firstEdge + i, 4); // surfedge
					int sebVal = BitConverter.ToInt32(bsp, seb);
					int edgeIdx = sebVal < 0 ? -sebVal : sebVal;
					int edgeVtx = sebVal < 0 ? 1 : 0;
					int vertex;
					if (!qbism) {
						int edgeVtxOfs = GetStructOfs(bsp, 11, edgeIdx, 4) + (edgeVtx * 2); // edges[edgeIdx][edgeVtx]
						vertex = BitConverter.ToUInt16(bsp, edgeVtxOfs);
					} else {
						int edgeVtxOfs = GetStructOfs(bsp, 11, edgeIdx, 8) + (edgeVtx * 4);
						vertex = BitConverter.ToInt32(bsp, edgeVtxOfs);
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
				List<ECLBSPFile.ModelTriangle> triangles = new();
				for (int i = 2; i < winding.Length; i++) {
					model.AddRenderable(new ECLBSPFile.ModelTriangle {
						tex = texInfoVal.Item1,
						a = winding[0],
						b = winding[i - 1],
						c = winding[i]
					}, area, areaTable);
				}
			}

			ECLBSPFile.Brush[] brushes;
			foreach ((int idx, int pos) in StructArray(bsp, 14, 12, out brushes)) {
				int firstSide = BitConverter.ToInt32(bsp, pos);
				int numSides = BitConverter.ToInt32(bsp, pos + 4);
				int contents = BitConverter.ToInt32(bsp, pos + 8);
				ECLBSPFile.BrushSide[] brushSides = new ECLBSPFile.BrushSide[numSides];
				for (int j = 0; j < numSides; j++) {
					Plane3d plane;
					(string, BrushUV) texInfo;
					if (!qbism) {
						int sidePos = GetStructOfs(bsp, 15, j + firstSide, 4);
						plane = planes[(int) BitConverter.ToUInt16(bsp, sidePos)];
						texInfo = GetTexInfoOrFallback((int) BitConverter.ToUInt16(bsp, sidePos + 2));
					} else {
						int sidePos = GetStructOfs(bsp, 15, j + firstSide, 8);
						plane = planes[BitConverter.ToInt32(bsp, sidePos)];
						texInfo = GetTexInfoOrFallback(BitConverter.ToInt32(bsp, sidePos + 4));
					}
					brushSides[j] = new ECLBSPFile.BrushSide {
						plane = plane,
						tex = texInfo.Item1,
						texUV = texInfo.Item2
					};
				}
				// CONTENTS_CURRENT_0
				// We use this as a 'secret handshake' to implement the 'noclip' brush.
				// Noclip brushes are solid (so block vis), but don't create collision.
				bool hasNoclipContents = (contents & 0x40000) != 0;
				// CONTENTS_SOLID | CONTENTS_PLAYERCLIP
				bool hasClipContents = (contents & (1 | 0x10000)) != 0;
				brushes[idx] = new ECLBSPFile.Brush {
					illusionary = hasNoclipContents || !hasClipContents,
					sides = brushSides
				};
			}

			// Despite lump order, models *must* be processed just before entities.
			// This is because we have to assemble lots of details out of everything else in the file.
			// Then, entities need to have models ready.

			/// Models. Entities point to these (or implicitly in the case of model 0, aka worldspawn).
			ECLBSPFile.Model[] models;
			foreach ((int idx, int pos) in StructArray(bsp, 13, 48, out models)) {
				int headNode = BitConverter.ToInt32(bsp, pos + 36);
				var model = new ECLBSPFile.Model {
					min = GetPosition(bsp, pos + 0),
					max = GetPosition(bsp, pos + 12),
					//origin = GetPosition(bsp, pos + 24, worldScale),
				};
				HashSet<int> brushNumSet = new();
				HashSet<int> faceNumSet = new();
				Dictionary<int, List<ECLBSPFile.ModelRenderable>> areaTable = new();
				void CollectLeaves(int node) {
					// For qbism format check https://github.com/qbism/q2tools-220/blob/3fcf535be656d8ff38a9b95238fc741f3aebbd09/src/qfiles.h#L575
					if (node < 0) {
						int area;
						int firstLeafFace;
						int numLeafFaces;
						int firstLeafBrush;
						int numLeafBrushes;
						if (!qbism) {
							// dleaf_t 4 + 2 + 2 + (3 * 2) + (3 * 2) + 2 + 2 + 2 + 2
							int leafOfs = GetStructOfs(bsp, 8, -(node + 1), 28);
							area = BitConverter.ToUInt16(bsp, leafOfs + 6);
							firstLeafFace = BitConverter.ToUInt16(bsp, leafOfs + 20);
							numLeafFaces = BitConverter.ToUInt16(bsp, leafOfs + 22);
							firstLeafBrush = BitConverter.ToUInt16(bsp, leafOfs + 24);
							numLeafBrushes = BitConverter.ToUInt16(bsp, leafOfs + 26);
						} else {
							// dleaf_tx 4 + 4 + 4 + (3 * 4) + (3 * 4) + 4 + 4 + 4 + 4
							int leafOfs = GetStructOfs(bsp, 8, -(node + 1), 52);
							area = BitConverter.ToInt32(bsp, leafOfs + 8);
							firstLeafFace = BitConverter.ToInt32(bsp, leafOfs + 36);
							numLeafFaces = BitConverter.ToInt32(bsp, leafOfs + 40);
							firstLeafBrush = BitConverter.ToInt32(bsp, leafOfs + 44);
							numLeafBrushes = BitConverter.ToInt32(bsp, leafOfs + 48);
						}
						for (int i = 0; i < numLeafFaces; i++) {
							int leafFace;
							if (!qbism) {
								int leafFaceOfs = GetStructOfs(bsp, 9, firstLeafFace + i, 2);
								leafFace = BitConverter.ToUInt16(bsp, leafFaceOfs);
							} else {
								int leafFaceOfs = GetStructOfs(bsp, 10, firstLeafFace + i, 4);
								leafFace = BitConverter.ToInt32(bsp, leafFaceOfs);
							}
							if (faceNumSet.Add(leafFace))
								AddFaceToModel(model, area, leafFace, areaTable);
						}
						for (int i = 0; i < numLeafBrushes; i++) {
							int leafBrush;
							if (!qbism) {
								int leafBrushOfs = GetStructOfs(bsp, 10, firstLeafBrush + i, 2);
								leafBrush = BitConverter.ToUInt16(bsp, leafBrushOfs);
							} else {
								int leafBrushOfs = GetStructOfs(bsp, 10, firstLeafBrush + i, 4);
								leafBrush = BitConverter.ToInt32(bsp, leafBrushOfs);
							}
							if (brushNumSet.Add(leafBrush))
								model.brushes.Add(brushes[leafBrush]);
						}
					} else {
						// the start of dnode_t and dnode_tx is the same
						int nodeOfs;
						if (!qbism) {
							// dnode_t 4 + (4 * 2) + (3 * 2) + (3 * 2) + 2 + 2
							nodeOfs = GetStructOfs(bsp, 4, node, 28);
						} else {
							// dnode_tx 4 + 8 + (3 * 4) + (3 * 4) + 4 + 4
							nodeOfs = GetStructOfs(bsp, 4, node, 44);
						}
						int plane = BitConverter.ToInt32(bsp, nodeOfs + 0);
						int childA = BitConverter.ToInt32(bsp, nodeOfs + 4);
						int childB = BitConverter.ToInt32(bsp, nodeOfs + 8);
						CollectLeaves(childA);
						CollectLeaves(childB);
					}
				}
				CollectLeaves(headNode);
				models[idx] = model;
			}

			int entsOffset = BitConverter.ToInt32(bsp, 8);
			int entsLength = BitConverter.ToInt32(bsp, 12);
			// We bet on treating NUL as whitespace.
			string entitiesLump = encoding.GetString(bsp, entsOffset, entsLength);

			// Parse entities and assign models.
			var entitiesParsed = MapParser.Parse(entitiesLump, (name) => name);
			var entParsedWorldspawn = EntityParsed<string>.EnsureWorldspawn(entitiesParsed);
			foreach (var entParsed in entitiesParsed) {
				// Start by finding the model.
				ECLBSPFile.Model entModel;
				string detectedModel = entParsed.pairs["model"];
				if (detectedModel.StartsWith("*")) {
					if (int.TryParse(detectedModel.Substring(1), out var result)) {
						if (result >= 0 && result < models.Length) {
							entModel = models[result];
						} else {
							entModel = null;
						}
					} else {
						entModel = null;
					}
				} else {
					entModel = (entParsed == entParsedWorldspawn) ? models[0] : null;
				}

				Vector3d origin = entParsed.pairs.GetVector3d("origin", Vector3d.Zero);
				// Create the entity.
				var entData = new ECLBSPFile.Entity(entParsed.pairs, entModel, origin);
				res.entities.Add(entData);
				if (entParsed == entParsedWorldspawn)
					res.worldspawn = entData;
			}

			return res;
		}

		// -- Geometry --
		private static Vector3d GetPosition(byte[] bsp, int pos) {
			float nX = BitConverter.ToSingle(bsp, pos);
			float nY = BitConverter.ToSingle(bsp, pos + 4);
			float nZ = BitConverter.ToSingle(bsp, pos + 8);
			return (nX, nY, nZ);
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
