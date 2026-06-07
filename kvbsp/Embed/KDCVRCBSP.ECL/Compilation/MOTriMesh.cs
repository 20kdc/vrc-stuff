using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace KDCVRCBSP.ECL {
	/// 'Mesh optimization' triangle.
	/// The 'tag' value covers anything that might cause two triangles not to be merged (such as texture mapping).
	public struct MOTri {
		public int vtxA, vtxB, vtxC, planeIndex, tag;

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool PlaneAndTagEq(MOTri other) => other.planeIndex == planeIndex && other.tag == tag;

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public int GetVertex(int idx) {
			switch (idx) {
				case 0: return vtxA;
				case 1: return vtxB;
				default: return vtxC;
			}
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public (int, int) GetLine(int idx) {
			switch (idx) {
				case 0: return (vtxA, vtxB);
				case 1: return (vtxB, vtxC);
				default: return (vtxC, vtxA);
			}
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public MOTri Arrange(int a, int b, int c) {
			return new MOTri {
				vtxA = a,
				vtxB = b,
				vtxC = c,
				planeIndex = planeIndex,
				tag = tag
			};
		}

		public (MOTri, MOTri) SplitAtLine(int lineIdx, int pointIdx) {
			switch (lineIdx) {
				// AB
				case 0: return (Arrange(vtxA, pointIdx, vtxC), Arrange(pointIdx, vtxB, vtxC));
				// BC
				case 1: return (Arrange(vtxA, vtxB, pointIdx), Arrange(vtxA, pointIdx, vtxC));
				// CA
				default: return (Arrange(pointIdx, vtxB, vtxC), Arrange(vtxA, vtxB, pointIdx));
			}
		}
	}

	public class MOTriMesh : IReadOnlyList<MOTri> {
		public readonly Geo2Context g2;
		private readonly List<MOTri> triangles = new();
		private readonly Dictionary<int, List<int>> vertexToTri = new();

		public MOTriMesh(Geo2Context g2) {
			this.g2 = g2;
		}

		// easy read access

		public int Count {
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => triangles.Count;
		}

		public MOTri this[int index] {
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => triangles[index];
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public IEnumerator<MOTri> GetEnumerator() => triangles.GetEnumerator();

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => triangles.GetEnumerator();

		// add/remove with indexing
		public void Add(MOTri tri) {
			int triIdx = triangles.Count;
			triangles.Add(tri);
			void AddTriToVertexList(int vtx) {
				if (vertexToTri.TryGetValue(vtx, out var list)) {
					list.Add(triIdx);
				} else {
					list = new();
					list.Add(triIdx);
					vertexToTri[vtx] = list;
				}
			}
			AddTriToVertexList(tri.vtxA);
			AddTriToVertexList(tri.vtxB);
			AddTriToVertexList(tri.vtxC);
		}

		public void Remove(int triIdx) {
			triangles.RemoveAt(triIdx);
			foreach (var val in vertexToTri.Values) {
				for (int i = 0; i < val.Count; i++) {
					int chkTri = val[i];
					if (chkTri == triIdx) {
						val.RemoveAt(i);
						i--;
					} else if (chkTri > triIdx) {
						val[i] = chkTri - 1;
					}
				}
			}
		}

		public IReadOnlyList<int> TrisForVertex(int vtx) {
			if (vertexToTri.TryGetValue(vtx, out var list)) {
				return list;
			} else {
				return Array.Empty<int>();
			}
		}
	}
}
