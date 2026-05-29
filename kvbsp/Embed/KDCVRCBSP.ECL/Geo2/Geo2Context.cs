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
		public double distanceEpsilon = 1d / 32d;

		/// Broadphase epsilon. This is supplied to AABB3d.Intersects.
		public double broadphaseEpsilon = 0.5d;

		/// Normal epsilon, used for comparing normals in co-planarity checks.
		/// The check expected here is Normal.Dot(OtherNormal) > normalEpsilon
		public double normalEpsilon = 1d - (1d / 32d);

		/// Initial winding size.
		public double initialWindingSize = 65536d;

		/// Debug switch to use a chop faces method that is liable to generate extra geometry.
		public bool debugChopFacesDoBadDirectCut = false;

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
				if ((pln.normal.Dot(plane.normal) > normalEpsilon) && (Math.Abs(pln.distance - plane.distance) < distanceEpsilon))
					return invert ? -(plnIdx + 1) : plnIdx;
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
	}
}