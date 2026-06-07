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
				throw new Exception("GenInitialWinding created bad winding for " + p + ", resulted in " + p2);
			return res;
		}

		public static List<Vector3d> FacePoints<D>(IEnumerable<Convex3d<D>.Face> faces) {
			List<Vector3d> lst = new();
			foreach (var face in faces)
				foreach (var point in face.winding)
					lst.Add(point);
			return lst;
		}

		public static bool PrepOnLine(Vector3d a, Vector3d b, double distanceEpsilon, out (Vector3d, Plane3d, double, double) prepared) {
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
			prepared = (minPoint, new Plane3d(rayNormal, minProgress), maxProgress - minProgress, distanceEpsilon);
			return rayNormal.Length != 0;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static (double, double) OnLineDist((Vector3d, Plane3d, double, double) prepared, Vector3d point) {
			// prepared.Item1: minPoint
			// prepared.Item2: rayPlane
			// prepared.Item3: rayLength
			// prepared.Item4: distanceEpsilon
			double pointProgress = prepared.Item2.SignedDistance(point);
			// point if it were as far along as minPoint
			Vector3d simulatedPoint = point - (prepared.Item2.normal * pointProgress);
			double pointDist = (prepared.Item1 - simulatedPoint).Length;
			return (pointDist, pointProgress);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool OnLine((Vector3d, Plane3d, double, double) prepared, bool includeEdges, Vector3d point) {
			var (pointDist, pointProgress) = OnLineDist(prepared, point);
			if (pointDist >= prepared.Item4)
				return false;
			if (includeEdges) {
				if (pointProgress < -prepared.Item4 || pointProgress > (prepared.Item3 + prepared.Item4))
					return false;
			} else {
				if (pointProgress < prepared.Item4 || pointProgress > (prepared.Item3 - prepared.Item4))
					return false;
			}
			return true;
		}

		public static Vector3d OnLineCross((Vector3d, Plane3d, double, double) preparedA, (Vector3d, Plane3d, double, double) preparedB) {
			var pointDistA0 = OnLineDist(preparedB, preparedA.Item1).Item1;
			var pointDistA1 = OnLineDist(preparedB, preparedA.Item1 + preparedA.Item2.normal).Item1;
			var travel1 = pointDistA1 - pointDistA0;
			return preparedA.Item1 - (preparedA.Item2.normal * (travel1 * pointDistA0));
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
