using System;

namespace KDCVRCBSP.ECL {
	/// Two doubles.
	public struct Vector2d {
		public double x, y;

		public Vector2d(double x, double y) {
			this.x = x;
			this.y = y;
		}

		public static implicit operator Vector2d((double, double) tuple) => new Vector2d(tuple.Item1, tuple.Item2);

		// -- VV --

		public static Vector2d operator +(Vector2d a, Vector2d b) => (a.x + b.x, a.y + b.y);

		public static Vector2d operator -(Vector2d a, Vector2d b) => (a.x - b.x, a.y - b.y);

		public static Vector2d operator *(Vector2d a, Vector2d b) => (a.x * b.x, a.y * b.y);

		public static Vector2d operator /(Vector2d a, Vector2d b) => (a.x / b.x, a.y / b.y);

		// -- VS --

		public static Vector2d operator +(Vector2d a, double b) => (a.x + b, a.y + b);

		public static Vector2d operator -(Vector2d a, double b) => (a.x - b, a.y - b);

		public static Vector2d operator *(Vector2d a, double b) => (a.x * b, a.y * b);

		public static Vector2d operator /(Vector2d a, double b) => (a.x / b, a.y / b);

		// -- Fancy ops --

		public double Length => Math.Sqrt((x * x) + (y * y));

		public Vector2d Normalized => (this.x == 0 && this.y == 0) ? new Vector2d(0, 0) : this / Length;

		public double Sum => x + y;

		public double Dot(Vector2d other) => (this * other).Sum;
	}
}