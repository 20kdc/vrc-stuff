using System;
using System.Collections.Generic;

namespace KDCVRCBSP.ECL {
	/// Mesh optimization algorithms.
	public static class MOAlgorithms {
		public static void FixTJunctions(MOTriMesh triMesh, Predicate<int> tagMayResolveTJuncs, List<int> tJuncPool) {
			var g2 = triMesh.g2;
			var distanceEpsilon = g2.epsilons.distance;
			var vertices = g2.PointsRaw;
			int triIdx = 0;
			while (triIdx < triMesh.Count) {
				var tri = triMesh[triIdx];
				if (!tagMayResolveTJuncs(tri.tag)) {
					triIdx++;
					continue;
				}
				bool didSplit = false;
				for (int edge = 0; edge < 3; edge++) {
					var (polyAIdxA, polyAIdxB) = tri.GetLine(edge);
					Vector3d polyAPointA = vertices[polyAIdxA];
					Vector3d polyAPointB = vertices[polyAIdxB];
					if (!GeomUtil.PrepOnLine(polyAPointA, polyAPointB, distanceEpsilon, out var linePrepared)) {
						// Console.WriteLine($"WARN: Zero-length polygon side at {polyAPointA}");
						// >:( get this deleted
						didSplit = true;
						break;
					}
					// go through points in the t-junction pool
					for (int pointPIdx = 0; pointPIdx < tJuncPool.Count; pointPIdx++) {
						var pointIdx = tJuncPool[pointPIdx];
						var point = vertices[pointIdx];
						if (!GeomUtil.OnLine(linePrepared, false, point))
							continue;
						// Point is definitely on line and is not an existing vertex.
						// Console.WriteLine($"Placed {point} between {polyAPointA} and {polyAPointB}!");
						var (triA, triB) = tri.SplitAtLine(edge, pointIdx);
						triMesh.Add(triA);
						triMesh.Add(triB);
						didSplit = true;
						break;
					}
					if (didSplit)
						break;
				}
				if (!didSplit) {
					triIdx++;
				} else {
					triMesh.Remove(triIdx);
				}
			}
		}

		/// Triangulates a concave surface.
		public static void TriangulateConcaveSurface(MOTriMesh mesh, IReadOnlyList<int> poly, MOTri specimen) {
			if (poly.Count < 3) {
				// shouldn't happen
				return;
			} else if (poly.Count == 3) {
				// simple triangle
				mesh.Add(specimen.Arrange(poly[0], poly[1], poly[2]));
				return;
			}
			var vertices = mesh.g2.PointsRaw;
			var distanceEpsilon = mesh.g2.epsilons.distance;
			// We arbitrarily need to decide on a pair of points through which we can cast a non-intersecting line.
			// This line cannot collide with any points along the way, nor can it collide with any line segments.
			// Consider a simple square, 4 points.
			// 1 2
			//
			// 4 3
			// The valid cuts here are (1, 3) [1, 2, 3][1, 3, 4] and (2, 4) [2, 3, 4] [2, 4, 1].
			for (int i = 0; i < poly.Count; i++) {
				for (int j = 0; j < poly.Count; j++) {
					if (i >= j - 1 && i <= j + 1)
						continue;
					bool cutValid = true;
					// Figure out if cut is valid.
					Vector3d qI = vertices[poly[i]];
					Vector3d qJ = vertices[poly[j]];
					// step 1: check there are no points on the line that are not intended to be there
					GeomUtil.PrepOnLine(qI, qJ, distanceEpsilon, out var preparedOuter);
					for (int q = 0; q < poly.Count; q++) {
						if (q == i || q == j)
							continue;
						if (GeomUtil.OnLine(preparedOuter, true, vertices[poly[q]])) {
							cutValid = false;
							break;
						}
					}
					if (!cutValid)
						continue;
					// step 2: check that the lines don't cross
					for (int q = 0; q < poly.Count; q++) {
						Vector3d qA = vertices[poly[q]];
						Vector3d qB = vertices[poly[(q + 1) % poly.Count]];
						GeomUtil.PrepOnLine(qA, qB, distanceEpsilon, out var preparedInner);
						var fix = GeomUtil.OnLineCross(preparedOuter, preparedInner);
						if (GeomUtil.OnLine(preparedOuter, true, fix)) {
							cutValid = false;
							break;
						}
					}
					// cut valid?
					if (!cutValid)
						continue;
					// The inside cut is just I through to J.
					// The outside cut is I, J, and then everything on the 'outside'.
					int x = i;
					List<int> insideList = new();
					List<int> outsideList = new();
					bool isInside = true;
					outsideList.Add(poly[i]);
					while (true) {
						if (isInside) {
							insideList.Add(poly[x]);
						} else {
							outsideList.Add(poly[x]);
						}
						if (x == j) {
							isInside = false;
							outsideList.Add(poly[x]);
						}
						x = (x + 1) % poly.Count;
						if (x == i)
							break;
					}
					TriangulateConcaveSurface(mesh, insideList, specimen);
					TriangulateConcaveSurface(mesh, outsideList, specimen);
					return;
				}
			}
			// :<
		}
	}
}
