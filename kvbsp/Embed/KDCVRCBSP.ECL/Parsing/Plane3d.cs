namespace KDCVRCBSP.ECL {
	/// Plane in 3D space.
	public struct Plane3d {
        public Vector3d normal;
		public double distance;

		public Plane3d(Vector3d normal, double distance) {
			this.normal = normal;
			this.distance = distance;
		}
	}
}