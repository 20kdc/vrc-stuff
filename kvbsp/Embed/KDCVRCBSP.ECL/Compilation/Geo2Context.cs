using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Runtime.CompilerServices;

namespace KDCVRCBSP.ECL {
	/// 'Geometry 2' context. Handles:
	/// 1. Plane equality speedup ('Planes lump')
	/// 2. Epsilons
	public class Geo2Context {
		private readonly List<Plane3d> planesRaw = new();

		/// All 'raw' planes in this context.
		/// These always have positive distances.
		public IReadOnlyList<Plane3d> PlanesRaw {
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => planesRaw;
		}

		private readonly List<Vector3d> pointsRaw = new();

		/// All 'raw' points in this context.
		public IReadOnlyList<Vector3d> PointsRaw {
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => pointsRaw;
		}

		public Vector3d this[int point] {
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => pointsRaw[point];
		}

		/// Epsilon config.
		public readonly Epsilons epsilons;

		public Geo2Context(Epsilons epsilons) {
			this.epsilons = epsilons;
		}

		/// Converts a plane to a plane index.
		/// A plane index is NOT a direct index into the PlanesRaw array.
		/// It may be negative, which indicates negation.
		public int ToPlaneIndex(Plane3d plane) {
			plane = new Plane3d(plane.normal.Normalized, plane.distance);
			bool invert = false;
			// Important: This is NOT enough to normalize all planes.
			// Planes with distance near 0 don't have this easy workaround.
			if (plane.distance < 0) {
				plane = plane.Flipped;
				invert = true;
			}

			// A 'slightly' misaligned plane could really mess things up, so we forcibly align such planes.
			var aaX = new Vector3d(Math.Sign(plane.normal.x), 0, 0);
			var aaY = new Vector3d(0, Math.Sign(plane.normal.y), 0);
			var aaZ = new Vector3d(0, 0, Math.Sign(plane.normal.z));
			if (epsilons.NormalNearNormal(plane.normal, aaX))
				plane.normal = new Vector3d(Math.Sign(plane.normal.x), 0, 0);
			else if (epsilons.NormalNearNormal(plane.normal, aaY))
				plane.normal = new Vector3d(0, Math.Sign(plane.normal.y), 0);
			else if (epsilons.NormalNearNormal(plane.normal, aaZ))
				plane.normal = new Vector3d(0, 0, Math.Sign(plane.normal.z));

			for (int plnIdx = 0; plnIdx < planesRaw.Count; plnIdx++) {
				var pln = planesRaw[plnIdx];
				if (epsilons.PlaneNearPlane(pln, plane))
					return invert ? -(plnIdx + 1) : plnIdx;
				else if (epsilons.PlaneNearPlane(pln, plane.Flipped))
					return invert ? plnIdx : -(plnIdx + 1);
			}

			int rawIdx = planesRaw.Count;
			planesRaw.Add(plane);
			return invert ? -(rawIdx + 1) : rawIdx;
		}

		/// Decodes a plane index.
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public Plane3d FromPlaneIndex(int idx) {
			return idx < 0 ? planesRaw[(-1) - idx].Flipped : planesRaw[idx];
		}

		/// Decodes a plane index and flips it.
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public Plane3d FromPlaneIndexFlipped(int idx) {
			return idx < 0 ? planesRaw[(-1) - idx] : planesRaw[idx].Flipped;
		}

		/// Flips a plane index.
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public int FlipPlaneIndex(int idx) => idx < 0 ? ((-1) - idx) : -(idx + 1);

		public int ToVertexIndex(Vector3d point) {
			for (int i = 0; i < pointsRaw.Count; i++)
				if ((pointsRaw[i] - point).Length < epsilons.distance)
					return i;
			var newVtx = pointsRaw.Count;
			pointsRaw.Add(point);
			return newVtx;
		}
	}
}
