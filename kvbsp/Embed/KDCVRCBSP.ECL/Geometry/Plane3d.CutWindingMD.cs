namespace KDCVRCBSP.ECL {
    using System;
    using System.Collections.Generic;
	using VectorD = Vector3d;
	using Self = Plane3d;

	public partial struct Plane3d {
		/// Cuts a winding, with metadata.
		/// Metadata is applied to the 'point A' of each line.
		/// The 'merge' operator applies for on-plane lines, taking (oldData, planeData).
		/// Something we have to concern ourselves a lot with here is the 'entry' and 'exit' points.
		/// In the below winding, the entry skips to the exit. In the above winding, the exit skips to the entry.
		public bool CutWindingMD<D>(List<(VectorD, D)> lst, List<(VectorD, D)> posLst, double epsilon, Func<D, D, D> merge, D planeData) {
			if (lst.Count == 0)
				return false;
			bool wasCut = false;
			// We store this because we'll need it for the line between the last and first point.
			var firstPoint = lst[0];
			int indexA = 0;
			while (indexA < lst.Count) {
				int indexB = indexA + 1;
				var pointA = lst[indexA];
				var pointB = (indexB == lst.Count) ? firstPoint : lst[indexB];
				// We handle the winding as a series of edges.
				// We're considered 'responsible' for the first point.
				double distA = SignedDistance(pointA.Item1);
				double distB = SignedDistance(pointB.Item1);
				int sideA = GeomUtil.SignedDistanceToSide(distA, epsilon);
				int sideB = GeomUtil.SignedDistanceToSide(distB, epsilon);

				// Perform early (non-intersect) metadata adjustment.
				// lst[indexA]: Below winding
				// pointA: Above winding
				// Since all of these happen with sideA == 0 check for that first.
				if (sideA == 0) {
					// This also means we run this only on-demand.
					var pointL = (indexA == 0) ? lst[lst.Count - 1] : lst[indexA - 1];
					double distL = SignedDistance(pointL.Item1);
					int sideL = GeomUtil.SignedDistanceToSide(distL, epsilon);
					if (sideB == 0)
						// If point A and B are on-plane, we have to merge to both targets (below & above).
						lst[indexA] = pointA = (pointA.Item1, merge(pointA.Item2, planeData));
					if (sideB > 0)
						// If point A is on-plane, but point B is above, the below winding will collapse the above geometry into one on-plane line.
						// But this will NOT happen for the above winding, which will create a line heading up!
						lst[indexA] = (pointA.Item1, planeData);
					if (sideL > 0)
						// If point L is above, and point A is on-plane, then the above winding has finished its excursion.
						// Ergo, we need to 'close the loop' for the above winding.
						pointA = (pointA.Item1, planeData);
				}

				// If point A is on or underneath the plane, it's in the final (below) geometry.
				// Otherwise, it's removed.
				if (sideA > 0) {
					lst.RemoveAt(indexA);
					wasCut = true;
				} else {
					indexA++;
				}
				// For posLst, the point is included if it's on or above the plane.
				if (sideA >= 0)
					posLst?.Add(pointA);

				// indexA is now the insertion point (if inserting) or the next index (if not).
				// The next question is if the line between points A and B crosses through the plane.
				if ((sideA < 0 && sideB > 0) || (sideA > 0 && sideB < 0)) {
					// Line crosses through plane.
					// One of these points will be deleted entirely.
					// The line that crosses back through the plane will generate its own intersection point.
					// Let's say that the line is headed positive. distA = -2, distB = 1.
					// Therefore, travel = 3, and the desired lerp value is 0.6666r.
					double travel = distB - distA;
					// Well, it's this.
					double lerpPtr = (-distA) / travel;
					VectorD intermediate = pointA.Item1.LerpUnclamped(pointB.Item1, lerpPtr);
					// Entering the above region, the below must emit plane data.
					// Exiting the above region, the above must emit plane data.
					// Otherwise, it keeps the original pointA value (since it's representing the entering/exiting line, not the crossconnect)
					D iDataBelow = (sideA < 0) ? planeData : pointA.Item2;
					D iDataAbove = (sideB < 0) ? planeData : pointA.Item2;
					lst.Insert(indexA, (intermediate, iDataBelow));
					posLst?.Add((intermediate, iDataAbove));
					wasCut = true;
					indexA++;
				}
			}
			return wasCut;
		}

		/// Turns a winding into a set of planes for the edges.
		public static List<(Self, D)> WindingToPlanesMD<D>(List<(VectorD, D)> winding, VectorD normal) {
			List<(Self, D)> planes = new();
			for (int i = 0; i < winding.Count; i++) {
				int j = (i + 1) % winding.Count;
				var a = winding[i];
				var b = winding[j];
				VectorD planeNormal = (a.Item1 - b.Item1).Cross(normal).Normalized;
				planes.Add((new Self(planeNormal, planeNormal.Dot(a.Item1)), a.Item2));
			}
			return planes;
		}
	}
}
