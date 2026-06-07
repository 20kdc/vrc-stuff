using System;
using System.Collections.Generic;

namespace KDCVRCBSP.ECL {
	/// Mesh optimization algorithms.
	public static class MOAlgorithms {
		public static void FixTJunctions(MOTriMesh triMesh, List<int> tJuncPool) {
			var g2 = triMesh.g2;
			var distanceEpsilon = g2.epsilons.distance;
			var vertices = g2.PointsRaw;
			int triIdx = 0;
			while (triIdx < triMesh.Count) {
				var tri = triMesh[triIdx];
				bool didSplit = false;
				for (int edge = 0; edge < 3; edge++) {
					var (polyAIdxA, polyAIdxB) = tri.GetLine(edge);
					Vector3d polyAPointA = vertices[polyAIdxA];
					Vector3d polyAPointB = vertices[polyAIdxB];
					var rayNormal = (polyAPointB - polyAPointA).Normalized;
					if (rayNormal.Length == 0) {
						// Console.WriteLine($"WARN: Zero-length polygon side at {polyAPointA}");
						// >:( get this deleted
						didSplit = true;
						break;
					}
					// go through points in the t-junction pool
					for (int pointPIdx = 0; pointPIdx < tJuncPool.Count; pointPIdx++) {
						var pointIdx = tJuncPool[pointPIdx];
						var point = vertices[pointIdx];
						// Console.WriteLine($"{rayNormal.Length} {polyAPointA} {polyAPointB}");
						double aProgress = rayNormal.Dot(polyAPointA);
						double pointProgress = rayNormal.Dot(point);
						// point if it were as far along as A
						Vector3d simulatedPoint = point + (rayNormal * (aProgress - pointProgress));
						double pointDist = (polyAPointA - simulatedPoint).Length;
						if (pointDist >= distanceEpsilon)
							continue;

						double bProgress = rayNormal.Dot(polyAPointB);
						double minProgress = Math.Min(aProgress, bProgress);
						double maxProgress = Math.Max(aProgress, bProgress);
						if (pointProgress < (minProgress + distanceEpsilon) || pointProgress > (maxProgress - distanceEpsilon))
							continue;
						// if (testPointEn && point.x == testPoint.x && point.y == testPoint.y && point.z == testPoint.z)
						//  Console.WriteLine("testPoint was hit in t-junc processing at " + polyAIndex + " in " + poly.Count);

						// Point is definitely on line and is not an existing vertex.
						//Console.WriteLine($"Placed {point} between {polyAPointA} and {polyAPointB}!");
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
	}
}
