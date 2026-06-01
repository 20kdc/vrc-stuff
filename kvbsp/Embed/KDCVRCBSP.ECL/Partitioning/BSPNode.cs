using System.Collections.Generic;

namespace KDCVRCBSP.ECL {
	/// Say the line!
    /// 'Binary space partitioning'.
    /// Yayyyy!!!!
	/// We use a node-as-object model in order to allow parallelism during the main split process.
	public abstract class BSPNode<D> {
		/// Build a BSP tree.
		/// Note the 'split history'. In a leaf node, this is turned directly into brushes.
		public static BSPNode<D> Build(Geo2Context g2, IReadOnlyList<Convex3d<D>.Face> splitFaces, IReadOnlyList<Convex3d<D>.Face> detailFaces, Convex3d<D>.Face[] splitHistory) {
			return null;
		}
		/// Split faces across two lists by a plane.
		public static (List<Convex3d<D>.Face>, List<Convex3d<D>.Face>) SplitFaces(Plane3d plane, IReadOnlyList<Convex3d<D>.Face> source) {
			List<Convex3d<D>.Face> below = new();
			List<Convex3d<D>.Face> above = new();
			return (below, above);
		}
	}
	/// BSP split.
	public class BSPSplit<D> {
		public readonly Convex3d<D>.Face splitFace;
		public readonly BSPNode<D> above;
		public readonly BSPNode<D> below;
	}
	public class BSPLeaf<D> {
		/// Faces inside this leaf.
		/// It was, somewhat arbitrarily, chosen that a face may only ever be inside one leaf.
		/// (On-plane faces end up in the leaf that agrees with their direction.)
		public readonly List<Convex3d<D>.Face> faces;
		/// This convex's data represents portals.
		/// It may otherwise be unenclosed.
		/// Notably, this list is dynamic; portalization happens later.
		public readonly Convex3d<List<BSPLeaf<D>>> convex;

		/// Create a BSP leaf from this convex and these faces.
		public BSPLeaf(Convex3d<List<BSPLeaf<D>>> convex, List<Convex3d<D>.Face> faces) {
			this.convex = convex;
			this.faces = faces;
		}
	}
}