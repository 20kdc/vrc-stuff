using System;
using System.Text;
using System.Collections.Generic;

namespace KDCVRCBSP.ECL {
	/// Loads a Quake 3 BSP file.
	/// TODO EVERYTHING, THIS IS COPY/PASTE OF THE Q2 LOADER FOR NOW TODO
	/// This handles the binary format details, skipping over parts of the format irrelevant to this use.
	public static class ECLQ3Loader {
		public const int CONTENTS_SOLID      = 0x00000001;
		public const int CONTENTS_PLAYERCLIP = 0x00010000;
		// disabled for now
		public const int CONTENTS_noclip  = 0x00000000;
		public static ECLBSPFile Load(byte[] bspRaw) {
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
			Vector3d[] vertexes = ECLLoadCom.HandleVertexLump(lumpVertexes);

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
				var pos = lumpFaces.GetStruct(face, 104);
				(string texture, _) = GetTexInfoOrFallback(pos.GetS32(0));
				int type = pos.GetS32(8);
				int firstVertex = pos.GetS32(12);
				int numVertices = pos.GetS32(16);
				int firstMeshvert = pos.GetS32(20);
				int numMeshverts = pos.GetS32(24);
				int patchW = pos.GetS32(96);
				int patchH = pos.GetS32(100);
				var meshvertsPos = lumpMeshverts.Subview(firstMeshvert * 4, numMeshverts * 4);

				// NOT a winding.
				ECLMesh.Vertex[] vertices = new ECLMesh.Vertex[numVertices];
				for (int i = 0; i < numVertices; i++) {
					var vertexPos = lumpVertexes.GetStruct(firstVertex + i, 44);
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
							vertexPos.GetF32(28),
							vertexPos.GetF32(32),
							vertexPos.GetF32(36)
						),
						colourR = vertexPos[40],
						colourG = vertexPos[41],
						colourB = vertexPos[42],
						colourA = vertexPos[43]
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
					for (int j = 0; j < patchH; j++)
						for (int i = 0; i < patchW; i++)
							patch.grid[i, j] = vertices[vtxIdx++];
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
				for (int j = 0; j < numSides; j++) {
					var sidePos = lumpBrushSides.GetStruct(j + firstSide, 8);
					Plane3d plane = planes[sidePos.GetS32(0)];
					(string texInfo, _) = GetTexInfoOrFallback(sidePos.GetS32(4));
					brushSides[j] = new ECLBSPFile.BrushSide {
						plane = plane,
						tex = texInfo,
						texUV = BrushUV.Fake(plane.normal)
					};
				}
				// We use this as a 'secret handshake' to implement the 'noclip' brush.
				// Noclip brushes are solid (so block vis), but don't create collision.
				bool hasNoclipContents = (contents & CONTENTS_noclip) != 0;
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
