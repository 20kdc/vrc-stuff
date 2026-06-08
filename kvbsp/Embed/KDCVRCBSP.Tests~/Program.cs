using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using KDCVRCBSP.ECL;

namespace KDCVRCBSP.Tests {
	public class Program {
		public static void Main(string[] args) {
			RunMapParsingTests();
			RunCutWindingTests();
			RunWindingToPlanesTests();
			RunOnLineTests();
		}

		public static void RunMapParsingTests() {
			Console.WriteLine("MapParsing test");

			List<string> lst;

			lst = MapParser.Tokenize("hello world [] [ ] \"some \\\\ \\\"stew\\\"\" // invisible\nvisible");
			Test.AssertListEq(lst, new string[] {
				"hello",
				"world",
				"[]",
				"[",
				"]",
				"some \\ \"stew\"",
				"visible"
			}.ToList(), "basic rules test");

			lst = MapParser.Tokenize("\"next_level\" \"Levels\\hallwaytohell.cmf\"");
			Test.AssertListEq(lst, new string[] {
				"next_level",
				"Levels\\hallwaytohell.cmf"
			}.ToList(), "Narbacular Drop filenames");

			// alright, let's parse these two complex maps to make sure they parse
			Console.WriteLine(" testmap id?");
			MapParser.Parse<string>(File.ReadAllText("testmap_id_tb.map"), (name) => name);
			Console.WriteLine(" testmap v220?");
			var mapchk = MapParser.Parse<string>(File.ReadAllText("testmap_220_hammer.map"), (name) => name);
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

		public static void RunWindingToPlanesTests() {
			Console.WriteLine("WindingToPlanes test");
			// ok, so, first, we need to create something that is actually winding-able
			Plane3d windingBasePlane = new Plane3d(new Vector3d(0, 0, 1), 0);
			List<Vector3d> winding = GeomUtil.GenInitialWinding(windingBasePlane, 64);
			new Plane3d(new Vector3d(1, 0, 0), 1).CutWinding(winding, null, 0.01d);
			new Plane3d(new Vector3d(-1, 0, 0), 2).CutWinding(winding, null, 0.01d);
			new Plane3d(new Vector3d(0, 1, 0), 3).CutWinding(winding, null, 0.01d);
			new Plane3d(new Vector3d(0, -1, 0), 4).CutWinding(winding, null, 0.01d);
			Test.AssertListEq(winding, new Vector3d[] {
				new Vector3d(1, 3, 0),
				new Vector3d(1, -4, 0),
				new Vector3d(-2, -4, 0),
				new Vector3d(-2, 3, 0)
			}, "QTP check stage 1 maths stable");
			// we should now have a quad with various sides at different distances
			// we now want to 'extract' those 4 planes from it, which we ought to get back
			var planes = Plane3d.WindingToPlanes(winding, windingBasePlane.normal);
			Test.AssertListEq(planes, new Plane3d[] {
				new Plane3d(new Vector3d(1, 0, 0), 1),
				new Plane3d(new Vector3d(0, -1, 0), 4),
				new Plane3d(new Vector3d(-1, 0, 0), 2),
				new Plane3d(new Vector3d(0, 1, 0), 3),
			}, "WindingToPlanes planes");
		}

		public static void RunOnLineTests() {
			Console.WriteLine("OnLine tests");
			GeomUtil.PrepOnLine((4, 1, 0), (8, 1, 0), out var prepared);
			GeomUtil.PrepOnLine((6, 2, 0), (6, 0, 0), out var preparedC);
			Test.Assert(GeomUtil.OnLine(prepared, 0.01d, 0.01d, false, (6, 1, 0)), "" + GeomUtil.OnLineDist(prepared, (6, 1, 0)));
			Test.Assert(!GeomUtil.OnLine(prepared, 0.01d, 0.01d, false, (6, 0, 0)), "" + GeomUtil.OnLineDist(prepared, (6, 0, 0)));
			Test.Assert(!GeomUtil.OnLine(prepared, 0.01d, 0.01d, false, (2, 1, 0)), "" + GeomUtil.OnLineDist(prepared, (2, 1, 0)));
			var crossTest = GeomUtil.OnLineCross(prepared, preparedC);
			Test.Assert(crossTest.x == 6, "crossTest == 6 1 0");
			Test.Assert(crossTest.y == 1, "crossTest == 6 1 0");
			Test.Assert(crossTest.z == 0, "crossTest == 6 1 0");
		}
	}
}
