using System;
using System.Collections.Generic;
using System.Linq;
using KDCVRCBSP.ECL;

namespace KDCVRCBSP.Tests {
    public class Program {
		public static void Main(string[] args) {

			Console.WriteLine("MapParsing test");
			List<string> lst = MapParsing.Tokenize("hello world [] [ ] \"some \\\\ \\\"stew\\\"\" // invisible\nvisible");
			AssertEq(lst[0], "hello", "lst");
			AssertEq(lst[1], "world", "lst");
			AssertEq(lst[2], "[]", "lst");
			AssertEq(lst[3], "[", "lst");
			AssertEq(lst[4], "]", "lst");
			AssertEq(lst[5], "some \\ \"stew\"", "lst");
			AssertEq(lst[6], "visible", "lst");
			AssertEq(lst.Count, 7, "lst count");

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
				Assert(new Plane2d(new Vector2d(1, 0), 4).CutWinding(pentagonCopy, 0.01d), "plane should have cut the pentagon");
				AssertEq(pentagonCopy[0], new Vector2d(4 + 0, 2), "pentagon 2d");
				AssertEq(pentagonCopy[1], new Vector2d(4 + 0, 0), "pentagon 2d");
				AssertEq(pentagonCopy[2], new Vector2d(4 + -1, 0), "pentagon 2d");
				AssertEq(pentagonCopy[3], new Vector2d(4 + -1, 1), "pentagon 2d");
				AssertEq(pentagonCopy.Count, 4, "pentagon 2d should have lost a vertex");
			}
			{
				// Run the 3D test.
				List<Vector3d> pentagonCopy = pentagonBase.Select(a => new Vector3d(a.x, a.y, a.x + a.y)).ToList();
				Assert(new Plane3d(new Vector3d(1, 0, 0), 4).CutWinding(pentagonCopy, 0.01d), "plane should have cut the pentagon");
				AssertEq(pentagonCopy[0], new Vector3d(4 + 0, 2, 6), "pentagon 3d");
				AssertEq(pentagonCopy[1], new Vector3d(4 + 0, 0, 4), "pentagon 3d");
				AssertEq(pentagonCopy[2], new Vector3d(4 + -1, 0, 3), "pentagon 3d");
				AssertEq(pentagonCopy[3], new Vector3d(4 + -1, 1, 4), "pentagon 3d");
				AssertEq(pentagonCopy.Count, 4, "pentagon 3d should have lost a vertex");
			}
		}

		public static void Assert(bool check, string rationale) {
			if (!check)
				throw new Exception("!!! ASSERT FAIL: " + rationale);
		}

		public static void AssertEq(object a, object b, string rationale) {
			Assert(a.Equals(b), rationale + " (" + a + " == " + b + ")");
		}
    }
}