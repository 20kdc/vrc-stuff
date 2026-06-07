using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

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
					if (!GeomUtil.PrepOnLine(polyAPointA, polyAPointB, out var linePrepared)) {
						// Console.WriteLine($"WARN: Zero-length polygon side at {polyAPointA}");
						// >:( get this deleted
						didSplit = true;
						break;
					}
					// go through points in the t-junction pool
					for (int pointPIdx = 0; pointPIdx < tJuncPool.Count; pointPIdx++) {
						var pointIdx = tJuncPool[pointPIdx];
						var point = vertices[pointIdx];
						if (!GeomUtil.OnLine(linePrepared, distanceEpsilon, distanceEpsilon, false, point))
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

		public static bool DiagnoseDupVertices(IReadOnlyList<int> poly, IBSPDiagnostics diag, string oopsie) {
			for (int q = 0; q < poly.Count; q++) {
				for (int x = q + 1; x < poly.Count; x++) {
					if (poly[q] != poly[x])
						continue;
					string dupVertInfo = "[";
					for (int r = 0; r < poly.Count; r++)
						dupVertInfo += " " + poly[r] + " ";
					dupVertInfo += "]";
					diag.Warning("MeshOpt surface contains duplicate vertex: " + dupVertInfo + " @ " + oopsie);
					return true;
				}
			}
			return false;
		}

		public struct Surface {
			public List<int> triangles;
			public MOTri specimen;
			public List<int> loopVertices;
			public int loopIndexOfVtx;
			public bool IsRemovable(MOTriMesh r) {
				if (loopIndexOfVtx == -1)
					return true;
				int vtxPrev = loopIndexOfVtx == 0 ? loopVertices[loopVertices.Count - 1] : loopVertices[loopIndexOfVtx - 1];
				int vtxCurr = loopVertices[loopIndexOfVtx];
				int vtxNext = loopIndexOfVtx == (loopVertices.Count - 1) ? loopVertices[0] : loopVertices[loopIndexOfVtx + 1];
				var pPrev = r.g2.PointsRaw[vtxPrev];
				var pCurr = r.g2.PointsRaw[vtxCurr];
				var pNext = r.g2.PointsRaw[vtxNext];
				GeomUtil.PrepOnLine(pPrev, pNext, out var prepLine);
				return GeomUtil.OnLine(prepLine, r.g2.epsilons.distance, r.g2.epsilons.distance, true, pCurr);
			}
		}

		/// Returns (newTri, newVtx), (-1, -1) for end of line, (-2, -2) for multiple.
		public static (int, int) Crawl(MOTriMesh triMesh, int commonVtx, int currentVtx, HashSet<int> seenTri, MOTri tri) {
			int advanced = 0;
			int currentResTri = -1;
			int currentResVtx = -1;
			foreach (int subTriIdx in triMesh.TrisForVertex(currentVtx)) {
				var subTri = triMesh[subTriIdx];
				if (!subTri.PlaneAndTagEq(tri))
					continue;
				if (!seenTri.Contains(subTriIdx)) {
					bool aTaken = subTri.vtxA == currentVtx || subTri.vtxA == commonVtx;
					bool bTaken = subTri.vtxB == currentVtx || subTri.vtxB == commonVtx;
					bool cTaken = subTri.vtxC == currentVtx || subTri.vtxC == commonVtx;
					int newVtx = -1;
					if (aTaken && bTaken)
						newVtx = subTri.vtxC;
					else if (bTaken && cTaken)
						newVtx = subTri.vtxA;
					else if (cTaken && aTaken)
						newVtx = subTri.vtxB;
					else
						continue;
					// next vertex found
					advanced++;
					currentResTri = subTriIdx;
					currentResVtx = newVtx;
				}
			}
			// too many advancements means something is off with the geometry
			if (advanced > 1)
				return (-2, -2);
			return (currentResTri, currentResVtx);
		}

		/// Find surfaces.
		/// If this returns null, the surfaces were not sufficiently coherent.
		public static List<Surface> FindSurfaces(MOTriMesh triMesh, int vtx, IBSPDiagnostics diag) {
			List<Surface> res = new();
			HashSet<int> seenTri = new();
			foreach (int triIdx in triMesh.TrisForVertex(vtx)) {
				if (seenTri.Add(triIdx)) {
					// alright, this is a new surface
					var tri = triMesh[triIdx];
					int lxA, lxB;
					if (tri.vtxA == vtx) {
						lxA = tri.vtxB;
						lxB = tri.vtxC;
					} else if (tri.vtxB == vtx) {
						lxA = tri.vtxC;
						lxB = tri.vtxA;
					} else if (tri.vtxC == vtx) {
						lxA = tri.vtxA;
						lxB = tri.vtxB;
					} else {
						diag.Warning("debug: vertex/tri cache mismatch");
						return null;
					}
					// lxA/lxB is the seed of the loop construction.
					Surface srf = new();
					srf.specimen = tri;
					srf.triangles = new();
					srf.triangles.Add(triIdx);
					srf.loopVertices = new();
					srf.loopVertices.Add(lxA);
					srf.loopVertices.Add(lxB);
					// expand backwards
					while (true) {
						var (newTri, newVtx) = Crawl(triMesh, vtx, srf.loopVertices[0], seenTri, tri);
						// too many advancements means something is off with the geometry
						if (newTri == -2)
							return null;
						if (newTri == -1)
							break;
						seenTri.Add(newTri);
						srf.triangles.Add(newTri);
						srf.loopVertices.Insert(0, newVtx);
					}
					// expand forwards
					while (true) {
						var (newTri, newVtx) = Crawl(triMesh, vtx, srf.loopVertices[srf.loopVertices.Count - 1], seenTri, tri);
						// too many advancements means something is off with the geometry
						if (newTri == -2)
							return null;
						if (newTri == -1)
							break;
						seenTri.Add(newTri);
						srf.triangles.Add(newTri);
						srf.loopVertices.Add(newVtx);
					}
					// if this is a complete standalone loop, let's see what would happen
					/*
					 * 1 2 3
					 * 8 0 4
					 * 7 6 5
					 * we start with tri 120. crawl forward (say) hits 230, 340, 450, 560, 670, 780, 810.
					 * so in bridge cases we will have have a loop that contains the same vertex twice.
					 */
					if (srf.loopVertices.Contains(vtx)) {
						diag.Warning("FindSurfaces loop contains vtx before bridge discovery.");
						return null;
					}
					if (srf.loopVertices[0] == srf.loopVertices[srf.loopVertices.Count - 1]) {
						// found bridge
						srf.loopVertices.RemoveAt(srf.loopVertices.Count - 1);
						srf.loopIndexOfVtx = -1;
						DiagnoseDupVertices(srf.loopVertices, diag, "FindSurfaces foundBridge: true");
					} else {
						srf.loopVertices.Insert(0, vtx);
						srf.loopIndexOfVtx = 0;
						DiagnoseDupVertices(srf.loopVertices, diag, "FindSurfaces foundBridge: false");
					}
					// and add
					res.Add(srf);
				}
			}
			return res;
		}

		/// Optimizes the mesh to remove unnecessary vertices.
		public static void OptimizeMesh(MOTriMesh triMesh, IBSPDiagnostics diag) {
			bool stillRunningPasses = true;
			while (stillRunningPasses) {
				stillRunningPasses = false;
				HashSet<int> seenVtx = new();
				for (int i = 0; i < triMesh.Count; i++) {
					var tri = triMesh[i];
					seenVtx.Add(tri.vtxA);
					seenVtx.Add(tri.vtxB);
					seenVtx.Add(tri.vtxC);
				}
				List<int> vtxIds = new(seenVtx);
				vtxIds.Sort();
				foreach (int maybeRemoveMe in vtxIds) {
					var surfaces = FindSurfaces(triMesh, maybeRemoveMe, diag);
					if (surfaces == null)
						continue;
					bool possible = true;
					foreach (var surface in surfaces) {
						if (!surface.IsRemovable(triMesh)) {
							possible = false;
							break;
						}
					}
					if (possible) {
						// prepare triangle removal list
						var triRemoval = new List<int>();
						// build new surfaces
						bool isCancelling = false;
						foreach (var surface in surfaces) {
							List<int> loopVerticesAdj = new(surface.loopVertices);
							if (surface.loopIndexOfVtx != -1)
								loopVerticesAdj.RemoveAt(surface.loopIndexOfVtx);
							if (loopVerticesAdj.Contains(maybeRemoveMe)) {
								diag.Warning("OOPS: new vertex list contained old vertex");
								isCancelling = true;
							}
							if (!TriangulateConcaveSurface(triMesh, loopVerticesAdj, surface.specimen, triRemoval, diag))
								isCancelling = true;
						}
						if (!isCancelling) {
							// committing to the new triangulation
							triRemoval.Clear();
							foreach (var surface in surfaces)
								foreach (var tri in surface.triangles)
									triRemoval.Add(tri);
							stillRunningPasses = true;
						}
						// in reverse due to index shifting
						triRemoval.Sort((a, b) => {
							if (a > b)
								return -1;
							else if (a < b)
								return 1;
							return 0;
						});
						foreach (int tri in triRemoval)
							triMesh.Remove(tri);
					}
				}
			}
		}

		/// Triangulates a concave surface.
		public static bool TriangulateConcaveSurface(MOTriMesh mesh, IReadOnlyList<int> poly, MOTri specimen, List<int> backstep, IBSPDiagnostics diag) {
			if (poly.Count < 3) {
				// shouldn't happen
				return false;
			} else if (poly.Count == 3) {
				// simple triangle
				backstep.Add(mesh.Add(specimen.Arrange(poly[0], poly[1], poly[2])));
				return true;
			}
			var vertices = mesh.g2.PointsRaw;
			var distanceEpsilon = mesh.g2.epsilons.distance;
			bool ConfirmCut(int i, int j) {
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
				if (!TriangulateConcaveSurface(mesh, insideList, specimen, backstep, diag))
					return false;
				return TriangulateConcaveSurface(mesh, outsideList, specimen, backstep, diag);
			}
			// We arbitrarily need to decide on a pair of points through which we can cast a non-intersecting line.
			// This line cannot collide with any points along the way, nor can it collide with any line segments.
			// Consider a simple square, 4 points.
			// 1 2
			//
			// 4 3
			// The valid cuts here are (1, 3) [1, 2, 3][1, 3, 4] and (2, 4) [2, 3, 4] [2, 4, 1].
			int test1fail = 0;
			int test2fail = 0;
			for (int i = 0; i < poly.Count; i++) {
				int iPrev = i == 0 ? poly.Count - 1 : i - 1;
				int iNext = i == poly.Count - 1 ? 0 : i + 1;
				for (int j = 0; j < poly.Count; j++) {
					if (j == i || j == iPrev || j == iNext)
						continue;
					bool cutValid = true;
					// Figure out if cut is valid.
					int pI = poly[i];
					int pJ = poly[j];
					Vector3d qI = vertices[pI];
					Vector3d qJ = vertices[pJ];
					// step 1: check there are no points on the line that are not intended to be there
					GeomUtil.PrepOnLine(qI, qJ, out var preparedOuter);
					for (int q = 0; q < poly.Count; q++) {
						if (q == i || q == j)
							continue;
						int pQ = poly[q];
						if (pQ == pI || pQ == pJ) {
							DiagnoseDupVertices(poly, diag, "TriangulateConcaveSurface");
							return false;
						}
						if (GeomUtil.OnLine(preparedOuter, distanceEpsilon, distanceEpsilon, true, vertices[poly[q]])) {
							cutValid = false;
							break;
						}
					}
					if (!cutValid) {
						test1fail++;
						continue;
					}
					// step 2: check that the lines don't cross
					// for angular reasons
					var edgeEpsilon = distanceEpsilon * 16;
					for (int q = 0; q < poly.Count; q++) {
						Vector3d qA = vertices[poly[q]];
						Vector3d qB = vertices[poly[(q + 1) % poly.Count]];
						GeomUtil.PrepOnLine(qA, qB, out var preparedInner);
						var fix = GeomUtil.OnLineCross(preparedOuter, preparedInner);
						if (GeomUtil.OnLine(preparedOuter, distanceEpsilon, edgeEpsilon, false, fix) && GeomUtil.OnLine(preparedInner, distanceEpsilon, edgeEpsilon, false, fix)) {
							cutValid = false;
							break;
						}
					}
					// cut valid?
					if (!cutValid) {
						test2fail++;
						continue;
					}
					return ConfirmCut(i, j);
				}
			}
			// :<
			diag.Warning($"WARN UNABLE TO FIND SPLIT, {poly.Count} points, {test1fail}, {test2fail}");
			return true;
		}
	}
}
