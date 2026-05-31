namespace KDCVRCBSP.ECL {
	using System.Collections.Generic;
	using VectorD = Vector3d;
	using Self = Plane3d;

    public partial struct Plane3d {
		/// Cuts a winding. Everything on the positive edge of the plane is lost.
		/// (If posLst is provided, a second winding is created there.)
		/// Winding order is maintained.
		/// If false is returned, the plane did not cut the winding.
		public bool CutWinding(List<VectorD> lst, List<VectorD> posLst, double epsilon) {
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
				double distA = SignedDistance(pointA);
				double distB = SignedDistance(pointB);
				int sideA = GeomUtil.SignedDistanceToSide(distA, epsilon);
				int sideB = GeomUtil.SignedDistanceToSide(distB, epsilon);
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
					VectorD intermediate = pointA.LerpUnclamped(pointB, lerpPtr);
					lst.Insert(indexA, intermediate);
					posLst?.Add(intermediate);
					wasCut = true;
					indexA++;
				}
			}
			return wasCut;
		}

		/// Turns a winding into a set of planes for the edges.
		public static List<Self> WindingToPlanes(List<VectorD> winding, VectorD normal) {
			List<Self> planes = new();
			for (int i = 0; i < winding.Count; i++) {
				int j = (i + 1) % winding.Count;
				var a = winding[i];
				var b = winding[j];
				VectorD planeNormal = (a - b).Cross(normal).Normalized;
				planes.Add(new Self(planeNormal, planeNormal.Dot(a)));
			}
			return planes;
		}
	}
}
