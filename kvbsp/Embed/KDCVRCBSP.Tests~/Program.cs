using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using KDCVRCBSP.ECL;

namespace KDCVRCBSP.Tests {
	public class Program {
		public static void Main(string[] args) {
			RunMapParsingTests();
			RunCutWindingTests();
		}

		public static void RunMapParsingTests() {
			Console.WriteLine("MapParsing test");

			List<string> lst;

			lst = MapParsing.Tokenize("hello world [] [ ] \"some \\\\ \\\"stew\\\"\" // invisible\nvisible");
			Test.AssertListEq(lst, new string[] {
				"hello",
				"world",
				"[]",
				"[",
				"]",
				"some \\ \"stew\"",
				"visible"
			}.ToList(), "basic rules test");

			lst = MapParsing.Tokenize("\"next_level\" \"Levels\\hallwaytohell.cmf\"");
			Test.AssertListEq(lst, new string[] {
				"next_level",
				"Levels\\hallwaytohell.cmf"
			}.ToList(), "Narbacular Drop filenames");

			// alright, let's parse these two complex maps to make sure they parse
			Console.WriteLine(" testmap id?");
			MapParsing.Parse(MapParsing.Tokenize(File.ReadAllText("testmap_id_tb.map")));
			Console.WriteLine(" testmap v220?");
			var mapchk = MapParsing.Parse(MapParsing.Tokenize(File.ReadAllText("testmap_220_hammer.map")));
			List<(string, List<List<Vector3d>>)> objsrc = new();
			int brushIdx = 0;
			foreach (var brush in mapchk[0].brushes) {
				List<Plane3d> facePlanes = new();
				foreach (var side in brush)
					facePlanes.Add(side.Plane);
				var faces = GeomUtil.DebugChopConvex(facePlanes.ToArray(), 0.125d);
				objsrc.Add(("b" + brushIdx, faces));
				brushIdx++;
			}
			File.WriteAllLines("../../../netbin/testmap_brushes.obj", GeomUtil.DebugMakeOBJ(objsrc));
		}

		public static void RunCutWindingTests() {
			Console.WriteLine("CutWinding test");
			// Draw an irregular pentagon.
			// The coordinate system you should be thinking of is Y-up, X-right.
			// We place the pentagon at X 4, Y 0. This is to catch any oddities with distance.
			List<Vector2d> pentagonBase = new();
			pentagonBase.Add((4 + 0, 2));
			pentagonBase.Add((4 + 1, 1));
			pentagonBase.Add((4 + 1, 0));
			pentagonBase.Add((4 + -1, 0));
			pentagonBase.Add((4 + -1, 1));
			{
				// Run the 2D test.
				List<Vector2d> pentagonCopy = pentagonBase.ToList();
				Test.Assert(new Plane2d(new Vector2d(1, 0), 4).CutWinding(pentagonCopy, null, 0.01d), "plane should have cut the pentagon");
				Test.AssertListEq(pentagonCopy, new Vector2d[] {
					new Vector2d(4 + 0, 2),
					new Vector2d(4 + 0, 0),
					new Vector2d(4 + -1, 0),
					new Vector2d(4 + -1, 1),
				}, "pentagon 2d");
			}
			{
				// Run the 3D test.
				List<Vector3d> pentagonCopy = pentagonBase.Select(a => new Vector3d(a.x, a.y, a.x + a.y)).ToList();
				Test.Assert(new Plane3d(new Vector3d(1, 0, 0), 4).CutWinding(pentagonCopy, null, 0.01d), "plane should have cut the pentagon");
				Test.AssertListEq(pentagonCopy, new Vector3d[] {
					new Vector3d(4 + 0, 2, 6),
					new Vector3d(4 + 0, 0, 4),
					new Vector3d(4 + -1, 0, 3),
					new Vector3d(4 + -1, 1, 4),
				}, "pentagon 3d");
			}
		}
	}
}
