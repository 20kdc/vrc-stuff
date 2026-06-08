using System;
using System.Text;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace KDCVRCBSP.ECL {
	/// Common loader code.
	public static class ECLLoadCom {
		public struct View {
			public byte[] data;
			public int ofs;
			public int len;

			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public View(byte[] data) {
				this.data = data;
				ofs = 0;
				len = data.Length;
			}

			public byte this[int idx] {
				[MethodImpl(MethodImplOptions.AggressiveInlining)]
				get => data[ofs + idx];
			}

			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public int GetS32(int ox) => BitConverter.ToInt32(data, ofs + ox);
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public short GetS16(int ox) => BitConverter.ToInt16(data, ofs + ox);
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public ushort GetU16(int ox) => BitConverter.ToUInt16(data, ofs + ox);
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public float GetF32(int ox) => BitConverter.ToSingle(data, ofs + ox);

			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public View Subview(int subofs, int sublen) => new View {
				data = data,
				ofs = ofs + subofs,
				len = sublen
			};

			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public View GetStruct(int index, int structLen) {
				int relOffset = index * structLen;
				if (relOffset < 0 || relOffset >= len)
					throw new Exception("Attempt to get object " + index + " -- this was out of range");
				return Subview(relOffset, structLen);
			}

			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			public (int, View)[] StructArray<T>(int structLen, out T[] target) {
				int count = len / structLen;
				target = new T[count];
				(int, View)[] locations = new (int, View)[count];
				for (int i = 0; i < count; i++)
					locations[i] = (i, Subview(i * structLen, structLen));
				return locations;
			}
		}

		public struct LumpTable {
			public View file;
			public int lumpHeaderOfsOfs, lumpHeaderLenOfs;
			public int lumpHeaderEntrySize;
			public View this[int idx] {
				[MethodImpl(MethodImplOptions.AggressiveInlining)]
				get {
					int adj = lumpHeaderEntrySize * idx;
					int lumpInfoOfsOfs = lumpHeaderOfsOfs + adj;
					int lumpInfoLenOfs = lumpHeaderLenOfs + adj;
					int ofs = file.GetS32(lumpInfoOfsOfs);
					int len = file.GetS32(lumpInfoLenOfs);
					return file.Subview(ofs, len);
				}
			}
		}

		/// Handles the planes lump.
		public static Plane3d[] HandlePlanesLump(View lumpPlanes, bool quake3) {
			// OmniSharp claims this can be inlined (it can't)
			Plane3d[] planes;
			foreach ((int idx, var pos) in lumpPlanes.StructArray(quake3 ? 16 : 20, out planes)) {
				float nX = pos.GetF32(0);
				float nY = pos.GetF32(4);
				float nZ = pos.GetF32(8);
				float d = pos.GetF32(12);
				planes[idx] = new Plane3d((nX, nY, nZ), d);
			}
			return planes;
		}

		public static Vector3d[] HandleVertexLump(View lumpVertexes) {
			Vector3d[] vertexes;
			foreach ((int idx, var pos) in lumpVertexes.StructArray(12, out vertexes)) {
				vertexes[idx] = (
					pos.GetF32(0),
					pos.GetF32(4),
					pos.GetF32(8)
				);
			}
			return vertexes;
		}

		public static ECLBSPFile ParseQuakeEntities(View lumpEntities, ECLBSPFile.Model[] models) {
			ECLBSPFile res = new();

			// We bet on treating NUL as whitespace.
			string entitiesText = new UTF8Encoding(false).GetString(lumpEntities.data, lumpEntities.ofs, lumpEntities.len);

			// Parse entities and assign models.
			var entitiesParsed = MapParser.Parse(entitiesText, (name) => name);
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

				// Create the entity.
				var entData = new ECLBSPFile.Entity(entParsed.pairs, entModel);
				res.entities.Add(entData);
				if (entParsed == entParsedWorldspawn)
					res.worldspawn = entData;
			}

			return res;
		}
	}
}
