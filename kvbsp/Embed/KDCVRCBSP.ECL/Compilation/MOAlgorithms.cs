namespace KDCVRCBSP.ECL {
	/// Mesh optimization algorithms.
	public static class MOAlgorithms {
		/*
						int polyAIndex = 0;
						while (polyAIndex < poly.Count) {
							Vector3d polyAPointA = vertices[poly[polyAIndex]];
							Vector3d polyAPointB = vertices[poly[(polyAIndex + 1) % poly.Count]];
							var rayNormal = (polyAPointB - polyAPointA).Normalized;
							if (rayNormal.Length == 0) {
								// Console.WriteLine($"WARN: Zero-length polygon side at {polyAPointA}");
								polyAIndex++;
								continue;
							}
							// go through points in the t-junction pool
							int pointIdx;
							for (pointIdx = 0; pointIdx < tJuncPool; pointIdx++) {
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
								if (testPointEn && point.x == testPoint.x && point.y == testPoint.y && point.z == testPoint.z)
									Console.WriteLine("testPoint was hit in t-junc processing at " + polyAIndex + " in " + poly.Count);
								// Point is definitely on line and is not an existing vertex.
								//Console.WriteLine($"Placed {point} between {polyAPointA} and {polyAPointB}!");
								poly.Insert(polyAIndex + 1, pointIdx);
								// we need to restart the outer logic given we have a new point B
								break;
							}
							// only advance if we didn't end up adding a point
							if (pointIdx == tJuncPool)
								polyAIndex++;
						}
		*/
	}
}
