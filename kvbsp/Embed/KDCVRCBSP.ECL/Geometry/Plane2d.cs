namespace KDCVRCBSP.ECL {
	using System.Collections.Generic;
	using System.Runtime.CompilerServices;
	using VectorD = Vector2d;

	/// Plane in 2D space.
	public struct Plane2d {
		/// Normal vector pointing 'upwards' relative to this plane.
		public VectorD normal;
		/// Distance.
		public double distance;

		public Plane2d(VectorD normal, double distance) {
			this.normal = normal;
			this.distance = distance;
		}

		/// Gets the signed distance of a point to this plane.
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public double SignedDistance(VectorD point) => point.Dot(normal) - distance;

		/// Gets the signed distance of a ray's origin to this plane along its normal.
		/// See SnapPointToPlaneAlongNormal for how this is used.
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public double RaySignedDistance(VectorD point, VectorD rayNormal) {
			// If this is 1, then it's equivalent to the plane normal (which has a 1:1 relation to SignedDistance).
			// If this is 0.5, then twice as much of the ray normal has to be 'travelled' to navigate up the plane.
			double travelDiv = normal.Dot(rayNormal);
			return SignedDistance(point) / travelDiv;
		}

		/// Snaps a point to this plane while constraining it to the given normal.
		/// This has various fascinating uses.
		/// For example, given a ray parallel to plane A, you can snap it onto plane B.
		/// This creates an intersecting point.
		/// Given the intersection normal, you now have a ray which encodes the intersection between A and B.
		/// This allows you to create one more (final) intersection, which completes a tri-plane intersection.
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public VectorD SnapPointToPlaneAlongNormal(VectorD point, VectorD snapNormal) {
			return point - (snapNormal * RaySignedDistance(point, snapNormal));
		}

		/// Cuts a winding. Everything on the positive edge of the plane is lost.
		/// Winding order is maintained.
		/// If false is returned, the plane did not intersect the winding.
		public bool CutWinding(List<VectorD> lst, double epsilon) {
			if (lst.Count == 0)
				return false;
			bool wasCut = false;
			// We store this because we'll need it for the line between the last and first point.
			VectorD firstPoint = lst[0];
			int indexA = 0;
			while (indexA < lst.Count) {
				int indexB = indexA + 1;
				VectorD pointA = lst[indexA];
				VectorD pointB = (indexB == lst.Count) ? firstPoint : lst[indexB];
				// We handle the winding as a series of edges.
				// We're considered 'responsible' for the first point.
				double distA = SignedDistance(pointA);
				double distB = SignedDistance(pointB);
				int sideA = GeomUtil.SignedDistanceToSide(distA, epsilon);
				int sideB = GeomUtil.SignedDistanceToSide(distB, epsilon);
				// If point A on or underneath the plane, it's in the final geometry.
				bool preserveA = sideA <= 0;
				if (!preserveA) {
					lst.RemoveAt(indexA);
					wasCut = true;
				} else {
					indexA++;
				}
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
					lst.Insert(indexA, pointA.LerpUnclamped(pointB, lerpPtr));
					wasCut = true;
					indexA++;
				}
			}
			return wasCut;
		}
	}
}