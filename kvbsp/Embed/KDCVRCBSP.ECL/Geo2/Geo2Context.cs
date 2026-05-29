using System.Collections.Generic;

namespace KDCVRCBSP.ECL {
	/// 'Geometry 2' context. Handles:
	/// 1. Plane equality speedup ('Planes lump')
	/// 2. Epsilons
	public sealed class Geo2Context {
		private readonly List<Plane3d> planes = new();
		/// All planes in this context.
		public IReadOnlyList<Plane3d> Planes => planes;
	}
}