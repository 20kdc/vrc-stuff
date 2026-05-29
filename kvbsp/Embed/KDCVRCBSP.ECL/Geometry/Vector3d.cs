using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace KDCVRCBSP.ECL {
	using Self = Vector3d;

	/// Three doubles.
	public struct Vector3d {
		public double x, y, z;

		public static Self Zero {
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => new Self(0, 0, 0);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public Vector3d(double x, double y, double z) {
			this.x = x;
			this.y = y;
			this.z = z;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static implicit operator Self((double, double, double) tuple) => new Self(tuple.Item1, tuple.Item2, tuple.Item3);

		/// Unit vector
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static explicit operator Self(double value) => new Self(value, value, value);

		public override string ToString() {
			return "V3d" + (x, y, z);
		}

		// -- VV --

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Self operator +(Self a, Self b) {
			return new Self(a.x + b.x, a.y + b.y, a.z + b.z);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Self operator -(Self a, Self b) {
			return new Self(a.x - b.x, a.y - b.y, a.z - b.z);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Self operator *(Self a, Self b) {
			return new Self(a.x * b.x, a.y * b.y, a.z * b.z);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Self operator /(Self a, Self b) {
			return new Self(a.x / b.x, a.y / b.y, a.z / b.z);
		}

		// -- VS --

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Self operator +(Self a, double b) => a + (Self) b;

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Self operator -(Self a, double b) => a - (Self) b;

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Self operator *(Self a, double b) => a * (Self) b;

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Self operator /(Self a, double b) => a / (Self) b;

		// -- 2D Planes --

		public Vector2d XY {
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => new Vector2d(x, y);
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			set {
				this.x = value.x;
				this.y = value.y;
			}
		}

		public Vector2d YX {
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => new Vector2d(y, x);
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			set {
				this.y = value.x;
				this.x = value.y;
			}
		}

		public Vector2d XZ {
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => new Vector2d(x, z);
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			set {
				this.x = value.x;
				this.z = value.y;
			}
		}

		public Vector2d ZX {
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => new Vector2d(z, x);
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			set {
				this.z = value.x;
				this.x = value.y;
			}
		}

		public Vector2d YZ {
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => new Vector2d(y, z);
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			set {
				this.y = value.x;
				this.z = value.y;
			}
		}

		public Vector2d ZY {
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => new Vector2d(z, y);
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			set {
				this.z = value.x;
				this.y = value.y;
			}
		}

		// -- Fancy ops base --

		public bool IsZero {
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => (this.x == 0 && this.y == 0 && this.z == 0);
		}

		public double Sum {
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => x + y + z;
		}

		public double Length {
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => Math.Sqrt((x * x) + (y * y) + (z * z));
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public Self Min(Self o) => new Self(Math.Min(x, o.x), Math.Min(y, o.y), Math.Min(z, o.z));

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public Self Max(Self o) => new Self(Math.Max(x, o.x), Math.Max(y, o.y), Math.Max(z, o.z));

		// -- Fancy ops --

		public Self Normalized {
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => IsZero ? ((Self) 0) : this / Length;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public double Dot(Self other) => (this * other).Sum;

		/// Linearly interpolate, unclamped.
		/// We use a method which should guarantee F=0 and F=1 equivalence regardless of rounding mode.
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public Self LerpUnclamped(Self other, double fac) => (this * (1 - fac)) + (other * fac);

		// -- Custom --

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public Vector3d Cross(Vector3d b) => new Vector3d {
			x = (y * b.z) - (z * b.y),
			y = (z * b.x) - (x * b.z),
			z = (x * b.y) - (y * b.x)
		};
	}
}