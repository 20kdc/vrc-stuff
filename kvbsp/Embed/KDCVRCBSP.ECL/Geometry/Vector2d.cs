using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace KDCVRCBSP.ECL {
	using Self = Vector2d;

	/// Two doubles.
	public struct Vector2d {
		public double x, y;

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public Vector2d(double x, double y) {
			this.x = x;
			this.y = y;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static implicit operator Self((double, double) tuple) => new Self(tuple.Item1, tuple.Item2);

		/// Unit vector
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static explicit operator Self(double value) => new Self(value, value);

		public override string ToString() {
			return "V2d" + (x, y);
		}

		// -- VV --

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Self operator +(Self a, Self b) {
			return new Self(a.x + b.x, a.y + b.y);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Self operator -(Self a, Self b) {
			return new Self(a.x - b.x, a.y - b.y);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Self operator *(Self a, Self b) {
			return new Self(a.x * b.x, a.y * b.y);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Self operator /(Self a, Self b) {
			return new Self(a.x / b.x, a.y / b.y);
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

		// -- Fancy ops base --

		public bool IsZero {
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => (this.x == 0 && this.y == 0);
		}

		public double Sum {
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => x + y;
		}

		public double Length {
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => Math.Sqrt((x * x) + (y * y));
		}

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

		public Self RotatedXMY {
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => new Self(-y, x);
		}

		public Self RotatedMXY {
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => new Self(y, -x);
		}
	}
}