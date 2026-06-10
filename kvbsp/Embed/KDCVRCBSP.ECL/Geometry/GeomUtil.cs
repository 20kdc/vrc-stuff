using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace KDCVRCBSP.ECL {
	/// Geometry utilities / maths extensions
	public static class GeomUtil {
		/// Returns which point a side is on relative to this plane.
		/// 1 is above, -1 is below, 0 is 'on' (within epsilon tolerance).
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static int SignedDistanceToSide(double dist, double epsilon) {
			if (Math.Abs(dist) < epsilon)
				return 0;
			if (dist < 0)
				return -1;
			return 1;			
		}

		/// Gets the normal of a winding.
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Vector3d GetWindingNormal(Vector3d vertexA, Vector3d vertexB, Vector3d vertexC) {
			return (vertexA - vertexB).Cross(vertexC - vertexB).Normalized;
		}

		/// Gets the normal of a winding (assuming it's valid i.e. has at least three points)
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Vector3d GetWindingNormal(List<Vector3d> winding) {
			return GetWindingNormal(winding[0], winding[1], winding[2]);
		}

		/// Creates a winding.
		public static List<Vector3d> GenInitialWinding(Plane3d p, double q) {
			// This algorithm in particular is subject to numerical stability issues.
			// If you're getting precision issues with collision: LOOK HERE FIRST!
			// The new 'travel vector' arrangement should offset this somewhat.
			// The old arrangement would leave quad points in somewhat random places.
			List<Vector3d> res = new();
			int primaryAxis = p.normal.PrimaryAxis;
			if (primaryAxis == 0) {
				// X-major
				var tv = p.NormalToTravelVector(new Vector3d(1, 0, 0));
				res.Add(p.SnapPointToPlaneUsingTravelVector(new Vector3d( 0,  q, -q), tv));
				res.Add(p.SnapPointToPlaneUsingTravelVector(new Vector3d( 0,  q,  q), tv));
				res.Add(p.SnapPointToPlaneUsingTravelVector(new Vector3d( 0, -q,  q), tv));
				res.Add(p.SnapPointToPlaneUsingTravelVector(new Vector3d( 0, -q, -q), tv));
			} else if (primaryAxis == 1) {
				// Y-major
				var tv = p.NormalToTravelVector(new Vector3d(0, 1, 0));
				res.Add(p.SnapPointToPlaneUsingTravelVector(new Vector3d( q,  0, -q), tv));
				res.Add(p.SnapPointToPlaneUsingTravelVector(new Vector3d( q,  0,  q), tv));
				res.Add(p.SnapPointToPlaneUsingTravelVector(new Vector3d(-q,  0,  q), tv));
				res.Add(p.SnapPointToPlaneUsingTravelVector(new Vector3d(-q,  0, -q), tv));
			} else {
				// Z-major
				var tv = p.NormalToTravelVector(new Vector3d(0, 0, 1));
				res.Add(p.SnapPointToPlaneUsingTravelVector(new Vector3d( q, -q,  0), tv));
				res.Add(p.SnapPointToPlaneUsingTravelVector(new Vector3d( q,  q,  0), tv));
				res.Add(p.SnapPointToPlaneUsingTravelVector(new Vector3d(-q,  q,  0), tv));
				res.Add(p.SnapPointToPlaneUsingTravelVector(new Vector3d(-q, -q,  0), tv));
			}
			// hacky way to do this without much more work
			if (p.normal.Dot(GetWindingNormal(res[0], res[1], res[2])) < 0.5d) {
				res.Reverse();
			}
			Plane3d p2 = new Plane3d(res[0], res[1], res[2]);
			// this is to catch REALLY wrong values only, so this isn't very tight.
			if ((p.normal.Dot(p2.normal) < 0.9d) || (Math.Abs(p.distance - p2.distance) > 0.1d))
				throw new Exception("GenInitialWinding created bad winding for " + p + ", resulted in " + p2 + ": " + res[0] + " " + res[1] + " " + res[2] + " pa" + primaryAxis + " q=" + q);
			return res;
		}

		public static bool PrepOnLine(Vector3d a, Vector3d b, out (Vector3d, Plane3d, double) prepared) {
			var rayNormal = (b - a).Normalized;
			// if (rayNormal.Length == 0)
			// >:(
			double aProgress = rayNormal.Dot(a);
			double bProgress = rayNormal.Dot(b);
			double minProgress;
			double maxProgress;
			Vector3d minPoint;
			if (aProgress < bProgress) {
				minPoint = a;
				minProgress = aProgress;
				maxProgress = bProgress;
			} else {
				minPoint = b;
				minProgress = bProgress;
				maxProgress = aProgress;
			}
			prepared = (minPoint, new Plane3d(rayNormal, minProgress), maxProgress - minProgress);
			return rayNormal.Length != 0;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static (double, double) OnLineDist((Vector3d, Plane3d, double) prepared, Vector3d point) {
			// prepared.Item1: minPoint
			// prepared.Item2: rayPlane
			// prepared.Item3: rayLength
			double pointProgress = prepared.Item2.SignedDistance(point);
			// point if it were as far along as minPoint
			Vector3d simulatedPoint = point - (prepared.Item2.normal * pointProgress);
			double pointDist = (prepared.Item1 - simulatedPoint).Length;
			return (pointDist, pointProgress);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool OnLine((Vector3d, Plane3d, double) prepared, double sideEpsilon, double edgeEpsilon, bool includeEdges, Vector3d point) {
			var (pointDist, pointProgress) = OnLineDist(prepared, point);
			if (pointDist >= sideEpsilon)
				return false;
			if (includeEdges) {
				if (pointProgress < -edgeEpsilon || pointProgress > (prepared.Item3 + edgeEpsilon))
					return false;
			} else {
				if (pointProgress < edgeEpsilon || pointProgress > (prepared.Item3 - edgeEpsilon))
					return false;
			}
			return true;
		}

		public static Vector3d OnLineCross((Vector3d, Plane3d, double) preparedA, (Vector3d, Plane3d, double) preparedB) {
			var pointDistA0 = OnLineDist(preparedB, preparedA.Item1).Item1;
			var pointDistA1 = OnLineDist(preparedB, preparedA.Item1 + preparedA.Item2.normal).Item1;
			var travel1 = pointDistA1 - pointDistA0;
			return preparedA.Item1 - (preparedA.Item2.normal * (travel1 * pointDistA0));
		}

		/// Triangulates a polygon, accounting for T-junction fixing and other such oddities.
		public static (int, int, int)[] TriangulateConvexPolygon(Vector3d[] positions, double distanceEpsilon) {
			if (positions.Length < 2)
				return new (int, int, int)[0];
			(int, int, int)[] triangulation = new (int, int, int)[positions.Length - 2];
			List<int> initLoop = new();
			for (int i = 0; i < positions.Length; i++)
				initLoop.Add(i);
			HashSet<ulong> failCache = new();
			if (!TriangulateConvexPolygon(positions, triangulation, 0, triangulation.Length, initLoop, failCache, distanceEpsilon)) {
				// Console.WriteLine("WARN TRIANGLE FAN FB");
				// fall back to triangle fan
				for (int i = 0; i < triangulation.Length; i++)
					triangulation[i] = (0, i + 1, i + 2);
			}
			return triangulation;
		}

		public static bool TriangulateConvexPolygon(Vector3d[] positions, (int, int, int)[] buffer, int bufferStart, int bufferLen, List<int> loop, HashSet<ulong> failCache, double distanceEpsilon) {
			if (loop.Count < 3)
				return false;
			ulong failCacheHash = 0;
			for (int i = 0; i < loop.Count; i++) {
				int v = loop[i];
				failCacheHash ^= (ulong) v;
				failCacheHash ^= failCacheHash << 56;
				failCacheHash >>= 4;
			}
			if (failCache.Contains(failCacheHash))
				return false;
			if (loop.Count == 3) {
				var a = loop[0];
				var ap = positions[a];
				var b = loop[1];
				var bp = positions[b];
				var c = loop[2];
				var cp = positions[c];
				if (bufferLen != 1)
					throw new Exception("Internal calculation error in TriangulatePolygon, bufferLen of tri != 1");
				PrepOnLine(positions[a], positions[b], out var prepared);
				if (OnLineDist(prepared, positions[c]).Item1 < distanceEpsilon) {
					// Console.WriteLine($"{a} {b} {c} fail");
					failCache.Add(failCacheHash);
					return false;
				}
				buffer[bufferStart] = (
					a,
					b,
					c
				);
				return true;
			}
			int pivotLen = (loop.Count - 4) / 2;
			if (pivotLen < 3)
				pivotLen = 3;
			for (int leftStart = 0; leftStart < loop.Count; leftStart++) {
				for (int leftLenAdj = 0; leftLenAdj < loop.Count - 4; leftLenAdj++) {
					// leftLen goes from 3 to loop.Count - 1.
					// However, there's a special reordering exception.
					int leftLen = leftLenAdj + 3;
					// We swap trying the 'pivot length' and 3.
					// 3 is not efficient for recursive breakdown as it means errors happen on the B side (late).
					if (leftLen == 3)
						leftLen = pivotLen;
					else if (leftLen == pivotLen)
						leftLen = 3;

					// split loop
					// +-I-+
					// |   |
					// +-J-+
					// start = 1, len = 4
					List<int> left = new();
					List<int> right = new();
					// inclusive end!
					int leftEnd = (leftStart + leftLen) - 1;
					// if leftEnd would be at loop.Count, then we want it to be (thus, include) vertex 0
					// note we don't use modulo here (else it eats all the vertices)
					int leftEndRemapped = leftEnd - loop.Count;
					for (int k = 0; k < loop.Count; k++) {
						if ((k <= leftStart || k >= leftEnd) && !(k < leftEndRemapped))
							right.Add(loop[k]);
						if ((k >= leftStart && k <= leftEnd) || (k <= leftEndRemapped))
							left.Add(loop[k]);
					}
					int leftTris = left.Count - 2;
					int rightTris = right.Count - 2;
					// this shouldn't happen, but if it did happen it'd be the beginning of an endless loop
					if (leftTris == 0 || rightTris == 0)
						continue;

					if (bufferLen != (leftTris + rightTris))
						throw new Exception($"Calculation error in TriangulatePolygon {leftTris} + {rightTris} != {bufferLen}");
					bool a = TriangulateConvexPolygon(positions, buffer, bufferStart, leftTris, left, failCache, distanceEpsilon);
					if (!a)
						continue;
					bool b = TriangulateConvexPolygon(positions, buffer, bufferStart + leftTris, rightTris, right, failCache, distanceEpsilon);
					if (b)
						return true;
				}
			}
			failCache.Add(failCacheHash);
			return false;
		}

		// -- Debug --

		/// OBJ test (to check winding chopper in practice)
		public static List<string> DebugMakeOBJ(List<(string, List<List<Vector3d>>)> objects) {
			int vertices = 0;
			List<string> res = new();
			foreach (var obj in objects) {
				res.Add($"o {obj.Item1}");
				foreach (var tri in obj.Item2) {
					var faceText = "f";
					foreach (var vtx in tri) {
						res.Add($"v {vtx.x} {vtx.y} {vtx.z}");
						vertices++;
						faceText += " " + vertices;
					}
					res.Add(faceText);
				}
			}
			return res;
		}

		/// Create portalfile.
		public static List<string> DebugMakePRT(List<(object, object, List<Vector3d>)> portals) {
			int leafCount = 0;
			Dictionary<object, int> leafNumbers = new();
			List<string> finale = new();
			foreach (var portal in portals) {
				int leafANo, leafBNo;
				if (leafNumbers.ContainsKey(portal.Item1)) {
					leafANo = leafNumbers[portal.Item1];
				} else {
					leafANo = leafCount++;
					leafNumbers[portal.Item1] = leafANo;
				}
				if (leafNumbers.ContainsKey(portal.Item2)) {
					leafBNo = leafNumbers[portal.Item2];
				} else {
					leafBNo = leafCount++;
					leafNumbers[portal.Item2] = leafBNo;
				}
				string totality = $"{portal.Item3.Count} {leafANo} {leafBNo}";
				foreach (var pt in portal.Item3)
					totality += $" {pt.x} {pt.y} {pt.z}";
				finale.Add(totality);
			}
			finale.Insert(0, "PRT1");
			finale.Insert(1, leafCount.ToString());
			finale.Insert(2, portals.Count.ToString());
			return finale;
		}

		/// Create leakfile.
		public static List<string> DebugMakePTS(List<Vector3d> route) {
			List<string> finale = new();
			foreach (var vec in route)
				finale.Add($"{vec.x} {vec.y} {vec.z}");
			return finale;
		}
	}
}
