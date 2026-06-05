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
		public double normalEpsilon = 1d / 256d;

		/// Initial winding size.
		public double initialWindingSize = 65536d;

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
			if (NormalNearNormal(plane.normal, aaX))
				plane.normal = new Vector3d(Math.Sign(plane.normal.x), 0, 0);
			else if (NormalNearNormal(plane.normal, aaY))
				plane.normal = new Vector3d(0, Math.Sign(plane.normal.y), 0);
			else if (NormalNearNormal(plane.normal, aaZ))
				plane.normal = new Vector3d(0, 0, Math.Sign(plane.normal.z));

			for (int plnIdx = 0; plnIdx < planesRaw.Count; plnIdx++) {
				var pln = planesRaw[plnIdx];
				if (PlaneNearPlane(pln, plane))
					return invert ? -(plnIdx + 1) : plnIdx;
				else if (PlaneNearPlane(pln, plane.Flipped))
					return invert ? plnIdx : -(plnIdx + 1);
			}

			int rawIdx = planesRaw.Count;
			planesRaw.Add(plane);
			return invert ? -(rawIdx + 1) : rawIdx;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool PlaneNearPlane(Plane3d a, Plane3d b) {
			return NormalNearNormal(a.normal, b.normal) && (Math.Abs(a.distance - b.distance) < distanceEpsilon);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool NormalNearNormal(Vector3d a, Vector3d b) {
			var comp = a - b;
			return Math.Abs(comp.x) < normalEpsilon && Math.Abs(comp.y) < normalEpsilon && Math.Abs(comp.z) < normalEpsilon;
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
