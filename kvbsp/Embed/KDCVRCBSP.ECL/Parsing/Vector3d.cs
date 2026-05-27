namespace KDCVRCBSP.ECL {
	/// Three doubles.
	public struct Vector3d {
		public double x, y, z;

		public Vector3d(double x, double y, double z) {
			this.x = x;
			this.y = y;
			this.z = z;
		}

		public static implicit operator Vector3d((double, double, double) tuple) => new Vector3d(tuple.Item1, tuple.Item2, tuple.Item3);

		// -- VV --

		public static Vector3d operator +(Vector3d a, Vector3d b) => (a.x + b.x, a.y + b.y, a.z + b.z);

		public static Vector3d operator -(Vector3d a, Vector3d b) => (a.x - b.x, a.y - b.y, a.z - b.z);

		public static Vector3d operator *(Vector3d a, Vector3d b) => (a.x * b.x, a.y * b.y, a.z * b.z);

		public static Vector3d operator /(Vector3d a, Vector3d b) => (a.x / b.x, a.y / b.y, a.z / b.z);

		// -- VS --

		public static Vector3d operator +(Vector3d a, double b) => (a.x + b, a.y + b, a.z + b);

		public static Vector3d operator -(Vector3d a, double b) => (a.x - b, a.y - b, a.z - b);

		public static Vector3d operator *(Vector3d a, double b) => (a.x * b, a.y * b, a.z * b);

		public static Vector3d operator /(Vector3d a, double b) => (a.x / b, a.y / b, a.z / b);

		// -- Other --

		public double Sum => x + y + z;

		public double Dot(Vector3d other) => (this * other).Sum;
	}
}