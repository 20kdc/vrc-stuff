using System;
using System.Collections.Generic;

namespace KDCVRCBSP.ECL {
	public static class MapTokenizer {
		/// Tokenizes the given .map data or entity lump.
		/// There are various different parsers, and none of them really totally agree.
		///
		/// Some useful notes:
		///
		/// * True QBSPs do not consider tokens as having types.
		///   This is to say, in a true QBSP, '"{"' is the same as '{'.
		/// * True vanilla QBSP1:
		///   * Knows about ';' and '#' line comments.
		///   * No string escapes or block comments.
		///   * Considers all bytes <= 32 to be whitespace.
		/// * True vanilla QBSP2:
		///   * Aware of QBSP1's comments plus '//' lines and '/*' blocks, but still not string escapes.
		///   * Same whitespace rule.
		/// * TrenchBroom:
		///   * Aware of '//' and ';' line comments, but not '/*' block comments.
		///     * Single '/' might get discarded. This never comes up in a valid map.
		///     * Considers /// to be a 'meaningful' (metadata) comment.
		///   * Extremely strict about whitespace; space and tab are the only valid whitespace.
		///   * TrenchBroom WILL WRITE '//' comments
		/// * This parser:
		///   * Knows about ';', '#', '//' line comments
		///   * No block comments (like QBSP1, TrenchBroom)
		///   * Considers all chars <= 32 to be whitespace.
		public static List<string> Tokenize(string entityLump) {
			char[] whitespace = new char[33];
			for (int i = 0; i < 33; i++)
				whitespace[i] = (char) i;
			List<string> tokens = new();
			while (entityLump.Length >= 0) {
				entityLump = entityLump.TrimStart(whitespace);
				if (entityLump.Length == 0)
					break;
				if (entityLump.StartsWith("//") || entityLump.StartsWith(";")) {
					// Something like a line comment that does not 'interrupt' an existing token is read as a line comment.
					int endPoint = entityLump.IndexOf('\n');
					if (endPoint == -1)
						break;
					entityLump = entityLump.Substring(endPoint + 1);
				} else if (entityLump.StartsWith("\"")) {
					entityLump = entityLump.Substring(1);
					// Quoted string.
					string token = "";
					while (entityLump.Length > 0) {
						// something not an escape
						int endPointQ = entityLump.IndexOf('\"');
						if (endPointQ == 0) {
							// end of string
							entityLump = entityLump.Substring(1);
							break;
						}
						int endPointE = entityLump.IndexOf('\\');
						if (endPointE == 0) {
							if (entityLump.Length >= 2) {
								// escape
								string escape = entityLump.Substring(1, 1);
								token += escape;
								entityLump = entityLump.Substring(2);
								continue;
							} else {
								// escape at end of stream
								token += entityLump;
								entityLump = "";
								break;
							}
						}
						// no immediate quote or escape
						int endPoint;
						if (endPointE == -1)
							endPoint = endPointQ;
						else if (endPointQ == -1)
							endPoint = endPointE;
						else
							endPoint = Math.Min(endPointE, endPointQ);

						if (endPoint == -1) {
							token += entityLump;
							entityLump = "";
							break;
						}
						string segment = entityLump.Substring(0, endPoint);
						entityLump = entityLump.Substring(endPoint);
						token += segment;
					}
					tokens.Add(token);
				} else {
					// Unquoted token.
					int endPoint = entityLump.IndexOfAny(whitespace);
					if (endPoint == -1) {
						tokens.Add(entityLump);
						break;
					} else {
						string token = entityLump.Substring(0, endPoint);
						// Console.WriteLine("debug: unquoted token " + token);
						tokens.Add(token);
						entityLump = entityLump.Substring(endPoint);
					}
				}
			}
			return tokens;
		}
	}
}
