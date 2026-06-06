using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Runtime.CompilerServices;

namespace KDCVRCBSP.ECL {
	/// Mesh optimization vertex pool.
	public class VertexPool {
		private readonly List<Vector3d> pointsRaw = new();

		/// All 'raw' planes in this context.
		/// These always have positive distances.
		public IReadOnlyList<Vector3d> PointsRaw {
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => pointsRaw;
		}

		public Vector3d this[int point] {
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => pointsRaw[point];
		}

		/// Distance epsilon.
		/// (Maybe this should be somehow shared with geo2context?)
		public double distanceEpsilon = 1d / 256d;

		public int ToVertexIndex(Vector3d point) {
			for (int i = 0; i < pointsRaw.Count; i++)
				if ((pointsRaw[i] - point).Length < distanceEpsilon)
					return i;
			var newVtx = pointsRaw.Count;
			pointsRaw.Add(point);
			return newVtx;
		}
	}
}
