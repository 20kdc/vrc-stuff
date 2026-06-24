using System;
using System.Text;
using System.Collections.Generic;

namespace KDCVRCBSP.ECL {
	/// Loads a Quake 3 BSP file.
	/// This handles the binary format details, skipping over parts of the format irrelevant to this use.
	public static class ECLQ3Loader {
		public const int CONTENTS_SOLID      = 0x00000001;
		public const int CONTENTS_PLAYERCLIP = 0x00010000;
		public static ECLBSPFile Load(byte[] bspRaw, bool raven) {
			var bsp = new ECLLoadCom.LumpTable {
				file = new(bspRaw),
				lumpHeaderEntrySize = 8,
				lumpHeaderOfsOfs = 8,
				lumpHeaderLenOfs = 12
			};

			// lumps
			var lumpEntities = bsp[0];
			var lumpTextures = bsp[1];
			var lumpPlanes = bsp[2];
			var lumpNodes = bsp[3];
			var lumpLeaves = bsp[4];
			var lumpLeafFaces = bsp[5];
			var lumpModels = bsp[7];
			var lumpBrushes = bsp[8];
			var lumpBrushSides = bsp[9];
			var lumpVertexes = bsp[10];
			var lumpMeshverts = bsp[11];
			var lumpFaces = bsp[13];

			Plane3d[] planes = ECLLoadCom.HandlePlanesLump(lumpPlanes, true);

			var encoding = new UTF8Encoding(false);

			(string, int)[] textures;
			foreach ((int idx, var pos) in lumpTextures.StructArray(64 + 8, out textures)) {
				int strofs = 0;
				int strlen = 0;
				while (strlen < 64) {
					if (pos[strofs + strlen] == 0)
						break;
					strlen++;
				}
				string texName = encoding.GetString(pos.data, pos.ofs + strofs, strlen);
				if (texName.StartsWith("textures/"))
					texName = texName.Substring(9);
				textures[idx] = (
					texName,
					// contents
					pos.GetS32(68)
				);
			}

			/// Gets TexInfo or a fake one.
			/// This is useful because nodraw faces don't necessarily have valid TexInfos.
			/// This comes up in collision processing.
			(string, int) GetTexInfoOrFallback(int i) {
				if (i >= 0 && i < textures.Length)
					return textures[i];
				return ("fallback", CONTENTS_SOLID);
			}

			// This is done in an inner function to ensure these are duplicated.
			// This reduces the chance of issues when translation occurs.
			void AddFaceToModel(ECLBSPFile.Model model, int area, int face, Dictionary<int, Dictionary<string, ECLBSPFile.ModelTriMesh>> areaTable) {
				int faceStructSize;
				if (!raven) {
					faceStructSize = 104;
				} else {
					faceStructSize = 148;
				}
				var pos = lumpFaces.GetStruct(face, faceStructSize);
				(string texture, _) = GetTexInfoOrFallback(pos.GetS32(0));
				int type = pos.GetS32(8);
				int firstVertex = pos.GetS32(12);
				int numVertices = pos.GetS32(16);
				int firstMeshvert = pos.GetS32(20);
				int numMeshverts = pos.GetS32(24);
				// luckily, we don't need anything in the middle of the 'lighting zone'
				int patchW = pos.GetS32(faceStructSize - 8);
				int patchH = pos.GetS32(faceStructSize - 4);
				var meshvertsPos = lumpMeshverts.Subview(firstMeshvert * 4, numMeshverts * 4);

				// NOT a winding.
				ECLMesh.Vertex[] vertices = new ECLMesh.Vertex[numVertices];
				for (int i = 0; i < numVertices; i++) {
					// dvertex_t/rdvertex_t in qfusion
					// the big deal here is lightstyles are back in Raven format
					// due to this, various data is available for each vertex!
					int vtxStructSize;
					int colourCount;
					if (!raven) {
						vtxStructSize = 44;
						colourCount = 1;
					} else {
						vtxStructSize = 80;
						colourCount = 4;
					}
					var vertexPos = lumpVertexes.GetStruct(firstVertex + i, vtxStructSize);
					// colours are the last element
					int colourBase = vtxStructSize - (colourCount * 4);
					// normal is always immediately before the first colour
					int normalBase = colourBase - (4 * 3);
					vertices[i] = new ECLMesh.Vertex {
						position = (
							vertexPos.GetF32(0),
							vertexPos.GetF32(4),
							vertexPos.GetF32(8)
						),
						// surface UVs only
						uv = (
							vertexPos.GetF32(12),
							vertexPos.GetF32(16)
						),
						normal = (
							vertexPos.GetF32(normalBase),
							vertexPos.GetF32(normalBase + 4),
							vertexPos.GetF32(normalBase + 8)
						),
						colourR = vertexPos[colourBase],
						colourG = vertexPos[colourBase + 1],
						colourB = vertexPos[colourBase + 2],
						colourA = vertexPos[colourBase + 3]
					};
				}

				if (type == 1 || type == 3) {
					// Polygon or mesh. Comes with a free triangulation, so don't try to invent one.
					for (int j = 0; j < numMeshverts - 2; j += 3) {
						int baseBP = j * 4;
						model.AddTri(texture, (
							vertices[meshvertsPos.GetS32(baseBP)],
							vertices[meshvertsPos.GetS32(baseBP + 4)],
							vertices[meshvertsPos.GetS32(baseBP + 8)]
						), area, areaTable);
					}
				} else if (type == 2) {
					// Bezier patch.
					if (patchW * patchH != vertices.Length)
						throw new Exception($"Bezier patch of size {patchW}, {patchH} did not match with vertex count {vertices.Length}.");
					var patch = new ECLBSPFile.ModelQ3Patch();
					patch.tex = texture;
					patch.concaveCollision = true;
					patch.grid = new ECLMesh.Vertex[patchW, patchH];
					int vtxIdx = 0;
					for (int j = 0; j < patchH; j++) {
						for (int i = 0; i < patchW; i++) {
							var vtx = vertices[vtxIdx++];
							vtx.uv2 = Vector2d.Zero;
							patch.grid[i, j] = vtx;
						}
					}
					for (int j = 0; j < patchH; j++) {
						for (int i = 0; i < patchW; i++) {
							var here = patch.grid[i, j];
							if (i != 0) {
								var left = patch.grid[i - 1, j];
								double dist = (left.position - here.position).Length;
								here.uv2.x = left.uv2.x + dist;
							}
							if (j != 0) {
								var up = patch.grid[i, j - 1];
								double dist = (up.position - here.position).Length;
								here.uv2.y = up.uv2.y + dist;
							}
							patch.grid[i, j] = here;
						}
					}
					// The above algorithm is too subject to 'torsion' in complex geometry. A shame, I liked it.
					// As it is, we use it to provide aspect-awareness to the original algorithm.
					Vector2d lmScale = patch.grid[patchW - 1, patchH - 1].uv2;
					for (int j = 0; j < patchH; j++) {
						for (int i = 0; i < patchW; i++) {
							var here = patch.grid[i, j];
							here.uv2 = new Vector2d(i / (double) patchW, j / (double) patchH) * lmScale;
							patch.grid[i, j] = here;
						}
					}
					model.renderables.Add(patch);
				} else {
					// Console.WriteLine("face of unknown type " + type);
				}
				// Console.WriteLine("face on model? " + type + " " + numVertices + " " + numMeshverts);
			}

			ECLBSPFile.Brush[] brushes;
			foreach ((int idx, var pos) in lumpBrushes.StructArray(12, out brushes)) {
				int firstSide = pos.GetS32(0);
				int numSides = pos.GetS32(4);
				(_, int contents) = GetTexInfoOrFallback(pos.GetS32(8));
				ECLBSPFile.BrushSide[] brushSides = new ECLBSPFile.BrushSide[numSides];
				// we fake noclip contents by probing brush sides for "common/noclip"
				// this is very bad, but it should work
				// preproom3 needs this for skybox reasons
				bool hasNoclipContents = false;
				for (int j = 0; j < numSides; j++) {
					// dbrushside_t / rdbrushside_t in qfusion qfiles
					// raven adds 'surfacenum' to the end
					var sidePos = lumpBrushSides.GetStruct(j + firstSide, raven ? 12 : 8);
					Plane3d plane = planes[sidePos.GetS32(0)];
					(string texInfo, _) = GetTexInfoOrFallback(sidePos.GetS32(4));
					if (texInfo == "common/noclip")
						hasNoclipContents = true;
					brushSides[j] = new ECLBSPFile.BrushSide {
						plane = plane,
						tex = texInfo,
						texUV = BrushUV.Fake(plane.normal)
					};
				}
				// We use this as a 'secret handshake' to implement the 'noclip' brush.
				// Noclip brushes are solid (so block vis), but don't create collision.
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
			foreach ((int idx, var pos) in lumpModels.StructArray(40, out models)) {
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
				};
				int firstFace = pos.GetS32(24);
				int numFaces = pos.GetS32(28);
				int firstBrush = pos.GetS32(32);
				int numBrushes = pos.GetS32(36);

				for (int i = 0; i < numBrushes; i++)
					model.brushes.Add(brushes[firstBrush + i]);

				Dictionary<int, Dictionary<string, ECLBSPFile.ModelTriMesh>> areaTable = new();
				// In Q3BSP, only the root model has a tree.
				if (idx == 0) {
					Plane3d[] axialPlanes = ECLLoadCom.FakeBrushesAxialPlanes(model.bounds);
					HashSet<int> faceNumSet = new();
					void CollectLeaves(int node, Plane3d[] splitChain) {
						if (node < 0) {
							// 12 * 4 = 48
							var leafOfs = lumpLeaves.GetStruct(-(node + 1), 48);
							int cluster = leafOfs.GetS32(0);
							int area = leafOfs.GetS32(4);
							// mins: 8, 12, 16
							// maxs: 20, 24, 28
							int firstLeafFace = leafOfs.GetS32(32);
							int numLeafFaces = leafOfs.GetS32(36);
							for (int i = 0; i < numLeafFaces; i++) {
								int leafFace = lumpLeafFaces.GetS32((firstLeafFace + i) * 4);
								if (faceNumSet.Add(leafFace))
									AddFaceToModel(model, area, leafFace, areaTable);
							}
							if (cluster >= 0)
								model.viewLeaves.Add(splitChain);
						} else {
							// the start of dnode_t and dnode_tx is the same
							// (3 * 4 = 12) + (6 * 4 = 24) = 36
							ECLLoadCom.View nodeOfs = lumpNodes.GetStruct(node, 36);
							int planeIndex = nodeOfs.GetS32(0);
							//Console.WriteLine(planeIndex);
							var plane = planes[planeIndex];
							int childA = nodeOfs.GetS32(4);
							int childB = nodeOfs.GetS32(8);
							CollectLeaves(childA, ECLLoadCom.AppendPlane(splitChain, plane.Flipped));
							CollectLeaves(childB, ECLLoadCom.AppendPlane(splitChain, plane));
						}
					}
					CollectLeaves(0, axialPlanes);
				} else {
					// For brush entities, we 'naively' collate all faces.
					for (int i = 0; i < numFaces; i++)
						AddFaceToModel(model, 0, firstFace + i, areaTable);
				}
				models[idx] = model;
			}

			var file = ECLLoadCom.ParseQuakeEntities(lumpEntities, models);
			file.uvPremultiplied = true;
			return file;
		}
	}
}
