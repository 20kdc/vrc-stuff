using System;
using System.Collections.Generic;
using KDCVRCBSP.ECL;

namespace KDCVRCBSP.Tests {
    public class Program {
		public static void Main(string[] args) {
			Console.WriteLine("class library tests");
			List<string> lst = MapTokenizer.Tokenize("hello world [] [ ] \"some \\\\ \\\"stew\\\"\" // invisible\nvisible");
			AssertEq(lst[0], "hello", "lst");
			AssertEq(lst[1], "world", "lst");
			AssertEq(lst[2], "[]", "lst");
			AssertEq(lst[3], "[", "lst");
			AssertEq(lst[4], "]", "lst");
			AssertEq(lst[5], "some \\ \"stew\"", "lst");
			AssertEq(lst[6], "visible", "lst");
			AssertEq(lst.Count, 7, "lst count");
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