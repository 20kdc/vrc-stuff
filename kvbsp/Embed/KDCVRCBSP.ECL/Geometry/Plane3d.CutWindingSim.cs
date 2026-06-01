namespace KDCVRCBSP.ECL {
	using System.Collections.Generic;
	using VectorD = Vector3d;

	public partial struct Plane3d {
		/// Simulates cutting a winding.
		public (bool, int, int) CutWindingSim(IReadOnlyList<VectorD> lst, double epsilon) {
			bool wasCut = false;
			int negCount = 0;
			int posCount = 0;
			// We store this because we'll need it for the line between the last and first point.
			var firstPoint = lst[0];
			for (int indexA = 0; indexA < lst.Count; indexA++) {
				int indexB = indexA + 1;
				var pointA = lst[indexA];
				var pointB = (indexB == lst.Count) ? firstPoint : lst[indexB];
				// We handle the winding as a series of edges.
				// We're considered 'responsible' for the first point.
				double distA = SignedDistance(pointA);
				double distB = SignedDistance(pointB);
				int sideA = GeomUtil.SignedDistanceToSide(distA, epsilon);
				int sideB = GeomUtil.SignedDistanceToSide(distB, epsilon);
				// If point A is on or underneath the plane, it's in the final (below) geometry.
				// Otherwise, it's removed.
				if (sideA <= 0) {
					negCount++;
				} else {
					wasCut = true;
				}
				// For posLst, the point is included if it's on or above the plane.
				if (sideA >= 0)
					posCount++;

				// indexA is now the insertion point (if inserting) or the next index (if not).
				// The next question is if the line between points A and B crosses through the plane.
				if ((sideA < 0 && sideB > 0) || (sideA > 0 && sideB < 0)) {
					negCount++;
					posCount++;
					wasCut = true;
				}
			}
			return (wasCut, negCount, posCount);
		}
	}
}
