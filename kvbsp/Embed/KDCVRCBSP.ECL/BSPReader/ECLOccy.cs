using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace KDCVRCBSP.ECL {
	/// Occy, aka the Occlusion Geometry Generator.
	/// In short, Occy works by expanding visleaves.
	public static class ECLOccy {
		public static List<Convex3d<bool>> IntoOcclusionGeometry(ECLBSPFile.Model model, double occlusionBorder, double worldBorder, double distanceEpsilon, double initialWindingSize, bool debugOccy) {
			var viewLeaves = model.viewLeaves;
			var brushes = model.brushes;
			bool parallel = true;
			bool worstSort = false;

			if (debugOccy)
				Console.WriteLine(" leaf count: " + viewLeaves.Count);

			// Stage 1: Sort leaves by tree depth.
			// The basic idea is that we assume the BSP tree is reasonably well-balanced.
			List<int> leafIndices = new();
			for (int i = 0; i < viewLeaves.Count; i++)
				leafIndices.Add(i);
			leafIndices.Sort((a, b) => {
				var ca = viewLeaves[a].Length;
				var cb = viewLeaves[b].Length;
				if (ca < cb)
					return worstSort ? 1 : -1;
				else if (ca > cb)
					return worstSort ? -1 : 1;
				else
					return 0;
			});

			// Stage 2: Construct ordered list of expanded view convexes.
			List<Convex3d<bool>> viewConvexes = new();

			Convex3d<bool> IntoExpandedConvex(Plane3d[] sourcePlanes) {
				var preBuildUnexpanded = Convex3d<bool>.FromPlanes(sourcePlanes, false, distanceEpsilon, initialWindingSize, true);
				// if this fails, give up now on this leaf.
				if (preBuildUnexpanded == null)
					return null;
				Plane3d[] expanded = new Plane3d[sourcePlanes.Length + 6];
				for (int j = 0; j < sourcePlanes.Length; j++)
					expanded[j] = new Plane3d(sourcePlanes[j].normal, sourcePlanes[j].distance + occlusionBorder);
				// axial bevelling
				for (int j = 0; j < 6; j++)
					expanded[sourcePlanes.Length + j] = preBuildUnexpanded.bounds.GenAxialPlane(j, occlusionBorder);
				var convex = Convex3d<bool>.FromPlanes(expanded, false, distanceEpsilon, initialWindingSize, true);
				if (convex == null || convex.faces.Count < 1)
					return null;
				return convex;
			}

			Stopwatch stp = new();
			stp.Start();
			if (!parallel) {
				foreach (var index in leafIndices) {
					var convex = IntoExpandedConvex(viewLeaves[index]);
					if (convex != null)
						viewConvexes.Add(convex);
				}
			} else {
				foreach (var expanded in leafIndices.AsParallel().AsOrdered().Select(leafIndex => IntoExpandedConvex(viewLeaves[leafIndex])))
					if (expanded != null)
						viewConvexes.Add(expanded);
			}
			stp.Stop();
			if (debugOccy)
				Console.WriteLine(" expandLeaves took " + stp.Elapsed);

			foreach (var brush in brushes) {
				if (!brush.occyViewpoint)
					continue;
				var convex = IntoExpandedConvex(brush.ToPlanes());
				if (convex != null)
					viewConvexes.Add(convex);
			}

			// Stage 3: Find view AABB to minimize damage to Unity occlusion builder.
			List<Convex3d<bool>> occluderGeo = new();
			if (viewConvexes.Count == 0)
				return occluderGeo;

			AABB3d viewBounds = AABB3d.Zero;
			for (int i = 0; i < viewConvexes.Count; i++) {
				if (i == 0)
					viewBounds = viewConvexes[i].bounds;
				else
					viewBounds = viewBounds.Join(viewConvexes[i].bounds);
			}

			// Stage 4: Start carving.
			var initiator = Convex3d<bool>.FromPlanes(new Plane3d[] {
				viewBounds.GenAxialPlane(0, worldBorder),
				viewBounds.GenAxialPlane(1, worldBorder),
				viewBounds.GenAxialPlane(2, worldBorder),
				viewBounds.GenAxialPlane(3, worldBorder),
				viewBounds.GenAxialPlane(4, worldBorder),
				viewBounds.GenAxialPlane(5, worldBorder),
			}, false, distanceEpsilon, initialWindingSize, true);

			if (initiator == null)
				return occluderGeo;
			occluderGeo.Add(initiator);

			stp.Reset();
			stp.Start();
			int processed = 0;
			foreach (var view in viewConvexes) {
				processed++;
				List<Convex3d<bool>> newGeo = new();
				if (parallel) {
					foreach (var subList in occluderGeo.AsParallel().AsOrdered().Select(occyUnderTest => {
						List<Convex3d<bool>> subList = new();
						OccyCutter(subList, view, occyUnderTest);
						return subList;
					}))
						newGeo.AddRange(subList);
				} else {
					foreach (var occyUnderTest in occluderGeo)
						OccyCutter(newGeo, view, occyUnderTest);
				}
				occluderGeo = newGeo;
			}
			stp.Stop();
			if (debugOccy)
				Console.WriteLine(" view count: " + processed + " occy count: " + occluderGeo.Count + " time: " + stp.Elapsed);
			return occluderGeo;
		}

		public static void OccyCutter(List<Convex3d<bool>> newGeo, Convex3d<bool> view, Convex3d<bool> occyUnderTest) {
			// broadphase
			if (!occyUnderTest.bounds.Intersects(view.bounds, 1d)) {
				newGeo.Add(occyUnderTest);
				return;
			}
			// check if actually intersecting
			bool completeEscape = false;
			foreach (var face in view.faces) {
				var (below, above) = occyUnderTest.Cut(face.plane, false);
				if (below == null) {
					completeEscape = true;
					break;
				}
			}
			if (completeEscape) {
				newGeo.Add(occyUnderTest);
				return;
			}
			// alright, it's intersecting, eliminate
			var occyHold = occyUnderTest;
			foreach (var face in view.faces) {
				if (occyHold == null)
					break;
				var (below, above) = occyHold.Cut(face.plane, false);
				if (above != null)
					newGeo.Add(above);
				occyHold = below;
			}
			// occyHold is within the leaf and is thus discarded.
		}
	}
}
