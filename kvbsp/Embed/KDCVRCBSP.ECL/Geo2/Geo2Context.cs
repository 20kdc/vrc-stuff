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

		/// Distance epsilon, used for comparing co-planarity in various circumstances (among other things)
		public double distanceEpsilon = 1d / 256d;

		/// Broadphase epsilon. This is supplied to AABB3d.Intersects.
		public double broadphaseEpsilon = 0.5d;

		/// Normal epsilon, used for comparing normals in co-planarity checks.
		/// The check expected here is Normal.Dot(OtherNormal) > normalEpsilon
		public double normalEpsilon = 1d - (1d / 256d);

		/// Initial winding size.
		public double initialWindingSize = 65536d;

		/// Converts a plane to a plane index.
		/// A plane index is NOT a direct index into the PlanesRaw array.
		/// It may be negative, which indicates negation.
		public int ToPlaneIndex(Plane3d plane) {
			plane = new Plane3d(plane.normal.Normalized, plane.distance);
			bool invert = false;
			if (plane.distance < 0) {
				plane = plane.Flipped;
				invert = true;
			}
			for (int plnIdx = 0; plnIdx < planesRaw.Count; plnIdx++) {
				var pln = planesRaw[plnIdx];
				if (PlaneNearPlane(pln, plane))
					return invert ? -(plnIdx + 1) : plnIdx;
			}

			// About to add a plane.
			// A 'slightly' misaligned plane could really mess things up, so we forcibly align such planes.
			var xaaDot = plane.normal.Dot(new Vector3d(1, 0, 0));
			var yaaDot = plane.normal.Dot(new Vector3d(0, 1, 0));
			var zaaDot = plane.normal.Dot(new Vector3d(0, 0, 1));
			if (xaaDot > normalEpsilon || xaaDot < -normalEpsilon)
				plane.normal = new Vector3d(Math.Sign(plane.normal.x), 0, 0);
			else if (yaaDot > normalEpsilon || yaaDot < -normalEpsilon)
				plane.normal = new Vector3d(0, Math.Sign(plane.normal.y), 0);
			else if (zaaDot > normalEpsilon || zaaDot < -normalEpsilon)
				plane.normal = new Vector3d(0, 0, Math.Sign(plane.normal.z));

			int rawIdx = planesRaw.Count;
			planesRaw.Add(plane);
			return invert ? -(rawIdx + 1) : rawIdx;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool PlaneNearPlane(Plane3d a, Plane3d b) {
			return (a.normal.Dot(b.normal) > normalEpsilon) && (Math.Abs(a.distance - b.distance) < distanceEpsilon);
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
	}
}