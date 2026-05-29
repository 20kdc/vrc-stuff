namespace KDCVRCBSP.ECL {
	using System.Collections.Generic;
	using System.Runtime.CompilerServices;
	using VectorD = Vector3d;
	using Self = Plane3d;

	/// Plane in 3D space.
	public struct Plane3d {
		/// Normal vector pointing 'upwards' relative to this plane.
		public VectorD normal;
		/// Distance.
		public double distance;

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public Plane3d(VectorD normal, double distance) {
			this.normal = normal;
			this.distance = distance;
		}

		/// Plane from vertices.
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public Plane3d(Vector3d vertexA, Vector3d vertexB, Vector3d vertexC) {
			// middle vertex is 'pivot'
			normal = GeomUtil.GetWindingNormal(vertexA, vertexB, vertexC);
			distance = normal.Dot(vertexB);
		}

		public override string ToString() {
			return "P3D" + (normal.x, normal.y, normal.z, distance);
		}

		/// Gets the signed distance of a point to this plane.
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public double SignedDistance(VectorD point) => point.Dot(normal) - distance;

		public Self Flipped {
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => new Self(normal * -1, -distance);
		}

		/// Snaps a point to this plane while constraining it to the given normal.
		/// This has various fascinating uses.
		/// For example, given a ray parallel to plane A, you can snap it onto plane B.
		/// This creates an intersecting point.
		/// Given the intersection ray normal, you now have a ray which encodes the intersection between A and B.
		/// This allows you to create one more (final) intersection, which completes a tri-plane intersection.
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public VectorD SnapPointToPlaneAlongNormal(VectorD point, VectorD snapNormal) {
			return SnapPointToPlaneUsingTravelVector(point, NormalToTravelVector(snapNormal));
		}

		/// Like SnapPointToPlaneAlongNormal, but assumes a pre-compiled travel vector.
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public VectorD SnapPointToPlaneUsingTravelVector(VectorD point, VectorD travelVector) {
			return point - (travelVector * SignedDistance(point));
		}

		/// This scales an intersecting normal into a 'travel vector'.
		/// Adding this to a point will increase that point's SignedDistance by 1.
		/// See SnapPointToPlaneAlongNormal for how this is used.
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public VectorD NormalToTravelVector(VectorD snapNormal) {
			return snapNormal / normal.Dot(snapNormal);
		}

		/// Cuts a winding. Everything on the positive edge of the plane is lost.
		/// (If posLst is provided, a second winding is created there.)
		/// Winding order is maintained.
		/// If false is returned, the plane did not cut the winding.
		public bool CutWinding(List<VectorD> lst, List<VectorD> posLst, double epsilon) {
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
					if (posLst != null)
						posLst.Add(pointA);

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
					if (posLst != null)
						posLst.Add(intermediate);
					wasCut = true;
					indexA++;
				}
			}
			return wasCut;
		}
	}
}
