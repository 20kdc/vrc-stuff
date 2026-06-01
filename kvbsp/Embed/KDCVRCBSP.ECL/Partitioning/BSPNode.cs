using System;
using System.Collections.Generic;
using System.Threading;

namespace KDCVRCBSP.ECL {
	/// Say the line!
    /// 'Binary space partitioning'.
    /// Yayyyy!!!!
	/// We use a node-as-object model in order to allow parallelism during the main split process.
	public abstract class BSPNode<D> {
		/// The face list is pre-sorted.
		/// This is a fast, but ugly, way of choosing the BSP splits.
		public static void PresortFaceList(List<Convex3d<D>.Face> faces) {
			// get the mean of all face centres
			Vector3d avg = new();
			foreach (var face in faces)
				avg += (face.bounds.min + face.bounds.max) / (2 * faces.Count);
			faces.Sort((a, b) => {
				Plane3d pa = a.g2.FromPlaneIndex(a.planeIndex);
				Plane3d pb = a.g2.FromPlaneIndex(b.planeIndex);
				// aa before not aa
				if (pa.IsAxisAligned) {
					if (!pb.IsAxisAligned)
						return -1;
				} else if (pb.IsAxisAligned) {
					return 1;
				}
				// sort by how it relates to the 'average point'.
				// this should create a nice situation in which we try to cleave from the 'middle-out'.
				double absa = Math.Abs(pa.SignedDistance(avg));
				double bbsa = Math.Abs(pb.SignedDistance(avg));
				if (absa < bbsa)
					return -1;
				else if (absa > bbsa)
					return 1;
				return 0;
			});
		}

		/// Build a BSP tree.
		/// Note the 'split history'. In a leaf node, this is turned directly into a convex.
		public static BSPNode<D> Build(Geo2Context g2, IReadOnlyList<Convex3d<D>.Face> splitFaces, IReadOnlyList<Convex3d<D>.Face> detailFaces, int[] splitHistory) {
			if (splitFaces.Count == 0) {
				var leafLists = new List<BSPPortal<D>>[splitHistory.Length];
				for (int i = 0; i < leafLists.Length; i++)
					leafLists[i] = new List<BSPPortal<D>>();
				Plane3d[] fixSplitHistory = new Plane3d[splitHistory.Length];
				for (int i = 0; i < fixSplitHistory.Length; i++)
					fixSplitHistory[i] = g2.FromPlaneIndex(splitHistory[i]);
				return new BSPLeaf<D>(Convex3d<List<BSPPortal<D>>>.FromPlanes(g2, fixSplitHistory, leafLists, true), detailFaces);
			}
			// Pick plane to cut by.
			int splitPlaneIndex = splitFaces[0].planeIndex;
			Plane3d splitPlane = g2.FromPlaneIndex(splitPlaneIndex);
			// Prep split history for subcalls.
			int[] newSplitHistoryBelow = new int[splitHistory.Length + 1];
			int[] newSplitHistoryAbove = new int[splitHistory.Length + 1];
			splitHistory.CopyTo(newSplitHistoryBelow, 0);
			splitHistory.CopyTo(newSplitHistoryAbove, 0);
			newSplitHistoryBelow[splitHistory.Length] = splitPlaneIndex;
			newSplitHistoryAbove[splitHistory.Length] = g2.FlipPlaneIndex(splitPlaneIndex);
			// Prepare lists.
			List<Convex3d<D>.Face> belowSplitFaces = new();
			List<Convex3d<D>.Face> belowDetailFaces = new();
			List<Convex3d<D>.Face> aboveSplitFaces = new();
			List<Convex3d<D>.Face> aboveDetailFaces = new();
			foreach (var face in splitFaces)
				TransferFace(splitPlane, splitPlaneIndex, face, belowSplitFaces, aboveSplitFaces, belowDetailFaces, aboveDetailFaces);
			foreach (var face in detailFaces)
				TransferFace(splitPlane, splitPlaneIndex, face, belowDetailFaces, aboveDetailFaces, belowDetailFaces, aboveDetailFaces);
			// this is making performance so much worse
			/*
			if (false) {
				// Build sub-nodes.
				// One node is built on this thread and the other is built on another thread.
				ManualResetEvent mre = new(false);
				BSPNode<D>[] below = new BSPNode<D>[1];
				ThreadPool.QueueUserWorkItem((_obj) => {
					below[0] = Build(g2, belowSplitFaces, belowDetailFaces, newSplitHistory);
					mre.Set();
				}, null);
				BSPNode<D> above = Build(g2, aboveSplitFaces, aboveDetailFaces, newSplitHistory);
				mre.WaitOne();
				return new BSPSplit<D>(splitPlane, below[0], above);
			} 
			*/
			BSPNode<D> below = Build(g2, belowSplitFaces, belowDetailFaces, newSplitHistoryBelow);
			BSPNode<D> above = Build(g2, aboveSplitFaces, aboveDetailFaces, newSplitHistoryAbove);
			return new BSPSplit<D>(splitPlane, below, above);
		}

		/// Transfers a face.
		/// Note the importance of the special 'on plane' class.
		/// This is how we prevent splitting by the same plane twice.
		public static void TransferFace(Plane3d splitPlane, int splitPlaneIndex, Convex3d<D>.Face face, List<Convex3d<D>.Face> below, List<Convex3d<D>.Face> above, List<Convex3d<D>.Face> belowOnPlane, List<Convex3d<D>.Face> aboveOnPlane) {
			if (face.planeIndex == splitPlaneIndex) {
				// On-plane, agrees with direction. Send to above.
				aboveOnPlane.Add(face);
				return;
			} else if (face.g2.FlipPlaneIndex(face.planeIndex) == splitPlaneIndex) {
				// On-plane, inverse direction. Send to below.
				belowOnPlane.Add(face);
				return;
			}
			// Determine which of below/above to punt this face to based on what a cut does.
			(int belowCount, int aboveCount) = splitPlane.CutWindingSim(face.winding, face.g2.distanceEpsilon);
			if (belowCount >= Convex3d<D>.WindingCollapseLimit)
				below.Add(face);
			if (aboveCount >= Convex3d<D>.WindingCollapseLimit)
				above.Add(face);
		}

		public abstract void AddLeaves(List<BSPLeaf<D>> leaves);

		public static void Portalize(IReadOnlyList<BSPLeaf<D>> leaves) {
			for (int i = 0; i < leaves.Count; i++) {
				var leafA = leaves[i];
				var leafABounds = leafA.convex.bounds;
				var g2 = leafA.convex.g2;
				for (int j = i + 1; j < leaves.Count; j++) {
					var leafB = leaves[j];
					var leafBBounds = leafB.convex.bounds;
					// First check: Do these leaves even maybe touch?
					if (!leafABounds.Intersects(leafBBounds, g2.broadphaseEpsilon))
						continue;
					// Ok, these leaves might touch.
					Dictionary<int, Convex3d<List<BSPPortal<D>>>.Face> leafAWindingMap = new();
					foreach (var leafAFace in leafA.convex.faces)
						leafAWindingMap[g2.FlipPlaneIndex(leafAFace.planeIndex)] = leafAFace;
					// Because leaves are convex and non-intersecting, there is a maximum of *ONE* touching plane.
					// We try to find that here.
					foreach (var leafBFace in leafB.convex.faces) {
						if (!leafAWindingMap.TryGetValue(leafBFace.planeIndex, out var leafAFace))
							continue;
						// It's possible only one plane is actually capable of getting here, but don't have a proof of that.
						// We now need to check for winding overlap.
						List<Vector3d> winding = new(leafAFace.winding);
						var leafBPlane = g2.FromPlaneIndex(leafBFace.planeIndex);
						var cutPlanes = Plane3d.WindingToPlanes(leafBFace.winding, leafBPlane.normal);
						foreach (Plane3d cutPlane in cutPlanes) {
							cutPlane.CutWinding(winding, null, g2.distanceEpsilon);
							if (winding.Count < Convex3d<D>.WindingCollapseLimit)
								break;
						}
						if (winding.Count < Convex3d<D>.WindingCollapseLimit)
							continue;
						// PORTAL CONFIRMED!
						var portal = new BSPPortal<D>(leafA, leafB, winding);
						leafAFace.data.Add(portal);
						leafBFace.data.Add(portal);
						break;
					}
				}
			}
		}

		public static List<string> MakeLeafOBJ(IReadOnlyList<BSPLeaf<D>> leaves) {
			List<(string, List<List<Vector3d>>)> objs = new();
			for (int i = 0; i < leaves.Count; i++) {
				List<List<Vector3d>> leafMesh = new();
				foreach (var cfx in leaves[i].convex.faces) {
					leafMesh.Add(new(cfx.winding));
				}
				objs.Add(("l" + i, leafMesh));
			}
			return GeomUtil.DebugMakeOBJ(objs);
		}

		public static List<string> MakePRT(IReadOnlyList<BSPLeaf<D>> leaves) {
			List<(object, object, List<Vector3d>)> portals = new();
			foreach (var leaf in leaves) {
				foreach (var side in leaf.convex.faces)
					foreach (var portal in side.data)
						// to deduplicate
						if (portal.below == leaf)
							portals.Add((leaf, portal.above, new List<Vector3d>(side.winding)));
			}
			return GeomUtil.DebugMakePRT(portals);
		}
	}
	/// BSP split.
	public sealed class BSPSplit<D> : BSPNode<D> {
		public readonly Plane3d plane;
		public readonly BSPNode<D> below, above;

		public BSPSplit(Plane3d plane, BSPNode<D> below, BSPNode<D> above) {
			this.plane = plane;
			this.below = below;
			this.above = above;
		}

		public override void AddLeaves(List<BSPLeaf<D>> leaves) {
			above.AddLeaves(leaves);
			below.AddLeaves(leaves);
		}
	}
	public sealed class BSPLeaf<D> : BSPNode<D> {
		/// Faces inside this leaf.
		/// It was, somewhat arbitrarily, chosen that a face may only ever be inside one leaf.
		/// (On-plane faces end up in the leaf that agrees with their direction.)
		public readonly IReadOnlyList<Convex3d<D>.Face> faces;
		/// This convex's data represents portals.
		/// It may otherwise be unenclosed.
		/// Notably, this list is dynamic; portalization happens later.
		public readonly Convex3d<List<BSPPortal<D>>> convex;

		/// Create a BSP leaf from this convex and these faces.
		public BSPLeaf(Convex3d<List<BSPPortal<D>>> convex, IReadOnlyList<Convex3d<D>.Face> faces) {
			this.convex = convex;
			this.faces = faces;
		}

		public override void AddLeaves(List<BSPLeaf<D>> leaves) {
			leaves.Add(this);
		}
	}

	/// Represents a portal between two leaves.
	/// Exists 'in the context of' its parent leaf.
	public sealed class BSPPortal<D> {
		public readonly BSPLeaf<D> below;
		public readonly BSPLeaf<D> above;
		public readonly IReadOnlyList<Vector3d> winding;
		public BSPPortal(BSPLeaf<D> below, BSPLeaf<D> above, IReadOnlyList<Vector3d> winding) {
			this.below = below;
			this.above = above;
			this.winding = winding;
		}
	}
}