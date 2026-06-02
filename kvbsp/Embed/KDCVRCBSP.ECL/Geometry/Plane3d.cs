namespace KDCVRCBSP.ECL {
	using System.Runtime.CompilerServices;
	using VectorD = Vector3d;
	using Self = Plane3d;

    /// Plane in 3D space.
    public partial struct Plane3d {
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

		public bool IsAxisAligned => (normal.x == 0 && normal.y == 0) || (normal.y == 0 && normal.z == 0) || (normal.z == 0 && normal.x == 0);

		/// Gets the signed distance of a point to this plane.
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public double SignedDistance(VectorD point) => point.Dot(normal) - distance;

		public Self Flipped {
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => new Self(normal * -1, -distance);
		}

		/// Snaps point to plane using plane normal.
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public VectorD SnapPointToPlane(VectorD point) {
			// The normal itself is a valid travel vector (it's rather how the whole construction works)
			return SnapPointToPlaneUsingTravelVector(point, normal);
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
	}
}
