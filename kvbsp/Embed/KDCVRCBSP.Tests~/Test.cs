using System;
using System.Collections.Generic;
using System.Linq;
using KDCVRCBSP.ECL;

namespace KDCVRCBSP.Tests {
    public static class Test {
		public static void Assert(bool check, string rationale) {
			if (!check)
				throw new Exception("!!! ASSERT FAIL: " + rationale);
		}

		public static void AssertEq(object a, object b, string rationale) {
			Assert(a.Equals(b), rationale + " (" + a + " == " + b + ")");
		}

		public static string ListStringify<T>(IEnumerable<T> a) {
			string aContent = "[";
			foreach (T v in a) {
				if (aContent != "[")
					aContent += ", ";
				aContent += v;
			}
			aContent += "]";
			return aContent;
		}

		public static void AssertListEq<T>(IEnumerable<T> a, IEnumerable<T> b, string rationale) {
			var aList = a.ToList();
			var bList = b.ToList();
            string errMsg = rationale + ": " + ListStringify(a) + " == " + ListStringify(b);
            Assert(aList.Count == bList.Count, errMsg);
            for (int i = 0; i < aList.Count; i++) {
                Assert(aList[i]!.Equals(bList[i]), errMsg);
            }
		}
    }
}