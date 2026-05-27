namespace KDCVRCBSP.ECL {
	/// Plane in 3D space.
	public struct Plane3d {
		/// Normal vector pointing 'upwards' relative to this plane.
        public Vector3d normal;
		/// Distance.
		public double distance;

		public Plane3d(Vector3d normal, double distance) {
			this.normal = normal;
			this.distance = distance;
		}

		/// Gets the signed distance of a point to this plane.
		double SignedDistance(Vector3d point) => point.Dot(normal) - distance;
	}
}