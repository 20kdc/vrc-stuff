using System;

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

		// -- Fancy ops --

		public double Length => Math.Sqrt((x * x) + (y * y) + (z * z));

		public Vector3d Normalized => (this.x == 0 && this.y == 0 && this.z == 0) ? new Vector3d(0, 0, 0) : this / Length;

		public double Sum => x + y + z;

		public double Dot(Vector3d other) => (this * other).Sum;

		// -- Custom --

		public Vector3d Cross(Vector3d b) => new Vector3d {
			x = (y * b.z) - (z * b.y),
			y = (z * b.x) - (x * b.z),
			z = (x * b.y) - (y * b.x)
		};
	}
}