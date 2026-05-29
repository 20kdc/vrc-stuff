using System;
using System.Collections.Generic;
using System.Security.Cryptography;

namespace KDCVRCBSP.ECL {
	public static class MapParsing {
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
		///   * Has string escapes, but will fallback if it doesn't understand an escape
		///     (This is a workaround for Narbacular Drop. '"next_level" "Levels\hallwaytohell.cmf"' indeed)
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
								// escape?
								string escape = entityLump.Substring(1, 1);
								// is this escape one we know?
								bool escapeAccepted = false;
								if (escape == "\\" || escape == "\"") {
									// verbatim
									escapeAccepted = true;
								}
								if (escapeAccepted) {
									token += escape;
									entityLump = entityLump.Substring(2);
								} else {
									// Narbacular Drop fix: Pretend unhandled escapes aren't escapes
									token += "\\";
									entityLump = entityLump.Substring(1);
								}
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

		/// Parses the given map.
		public static List<EntityParsed> Parse(IEnumerable<string> tokens) => Parse(tokens.GetEnumerator());

		/// Parses the given map.
		public static List<EntityParsed> Parse(IEnumerator<string> tokens) {
			List<EntityParsed> result = new();
			EntityParsed hasEntity = null;
			string hasKey = null;
			Vector3d[] pointBuf = new Vector3d[3];
			double[] stBuf = new double[8];
			while (tokens.MoveNext()) {
				string token = tokens.Current;
				if (hasEntity != null) {
					if (hasKey != null) {
						// value takes precedence to allow for "{" and "}" as values
						hasEntity.pairs.Add((hasKey, token));
						hasKey = null;
					} else if (token == "{") {
						// "{" as 'key': start brush
						List<EntityParsed.BrushSide> brush = new();
						// expected side forms:
						// ID  : ( x y z ) ( x y z ) ( x y z ) texture xshift yshift rotation xscale yscale
						// V220: ( x y z ) ( x y z ) ( x y z ) texture [ x y z w ] [ x y z w ] rotation xscale yscale
						while (true) {
							// skip until we reach "(" (next brushside start) or "}" (end of brush)
							while (true) {
								// if we hit early EOF, we abandon this brush.
								// the entity will remain in the result
								if (!tokens.MoveNext())
									return result;
								token = tokens.Current;
								if (token == "(" || token == "}")
									break;
							}
							// If brush end, break (thus adding brush), otherwise we assume this is "("
							if (token == "}")
								break;
							for (int i = 0; i < 3; i++) {
								// Note we only check '(' for the second/third vectors
								// This is because of the check above for end of brush.
								if (i != 0)
									tokens.MoveNext(); // (

								if (!tokens.MoveNext())
									return result;
								double x1 = double.Parse(tokens.Current);
								if (!tokens.MoveNext())
									return result;
								double y1 = double.Parse(tokens.Current);
								if (!tokens.MoveNext())
									return result;
								double z1 = double.Parse(tokens.Current);
								pointBuf[i] = new Vector3d(x1, y1, z1);

								if (!tokens.MoveNext()) // )
									return result;
							}
							// texture
							if (!tokens.MoveNext())
								return result;
							string texture = tokens.Current;
							// Gets filled in with texture info
							EntityParsed.BrushSide brushSide = new EntityParsed.BrushSide {
								vertexA = pointBuf[0],
								vertexB = pointBuf[1],
								vertexC = pointBuf[2],
								texture = texture
							};
							// ID  : xshift yshift rotation xscale yscale
							// V220: [ x y z w ] [ x y z w ] rotation xscale yscale
							// notably:
							//  * Q2 adds extra metadata we don't care about.
							//  * rotation is basically useless in V220 (editor-only). but xscale/yscale isn't!
							//  * everything's arranged in such a way that the 'smart move' is to apply xscale/yscale always
							if (!tokens.MoveNext())
								return result;
							if (tokens.Current == "[") {
								// V220: [ x y z w ] [ x y z w ] rotation
								for (int q = 0; q < 8; q++) {
									if (!tokens.MoveNext())
										return result;
									stBuf[q] = double.Parse(tokens.Current);
									if (q == 3) {
										if (!tokens.MoveNext()) // ]
											return result;
										if (tokens.Current != "]")
											throw new Exception("Expected ], got " + tokens.Current);
										if (!tokens.MoveNext()) // [
											return result;
										if (tokens.Current != "[")
											throw new Exception("Expected [, got " + tokens.Current);
									}
								}
								if (!tokens.MoveNext()) // ]
									return result;
								if (tokens.Current != "]")
									throw new Exception("Expected ], got " + tokens.Current);
								// remaining: rotation
								// we skip rotation
								if (!tokens.MoveNext())
									return result;
								double.Parse(tokens.Current);
								brushSide.texSAxis = new Vector3d(stBuf[0], stBuf[1], stBuf[2]);
								brushSide.texTAxis = new Vector3d(stBuf[4], stBuf[5], stBuf[6]);
								brushSide.texOffset = new Vector2d(stBuf[3], stBuf[7]);
							} else {
								// ** DO NOT RELY ON THIS CODE. **
								// (It needs some tweaks that probably aren't coming)

								// ID: xshift yshift rotation
								// initial MoveNext does not happen because current token is already xshift
								double xShift = double.Parse(tokens.Current);
								if (!tokens.MoveNext())
									return result;
								double yShift = double.Parse(tokens.Current);
								if (!tokens.MoveNext())
									return result;
								double rotation = double.Parse(tokens.Current);

								double rotRadians = (rotation / 180.0) * Math.PI;
								double sin = Math.Sin(rotRadians);
								double cos = Math.Cos(rotRadians);

								// figure out 'texture axis'
								var normal = brushSide.Plane.normal;
								double naX = Math.Abs(normal.x);
								double naY = Math.Abs(normal.y);
								double naZ = Math.Abs(normal.z);

								// in conflict, Z should win, then X
								// consider: baseaxis table at QBSP/MAP.C#L211, columns 2 and 3
								//  (first column is for their dot-based detection which is ike mute)
								//  and that cosv = 1 for rotation = 0
								// it follows that:
								// 'cos' here maps to 1 values in the corresponding column of the baseaxis table
								// 'sin' here maps to 1 values in the opposite column
								//  -1 values become -cos, -sin
								(Vector3d sAxis, Vector3d tAxis) =
									(naY > naX && naY > naZ) ?
										((cos, 0d, -sin), (sin, 0d, -cos)) // Y wins
									: ((naX > naZ) ?
										((0d, cos, -sin), (0d, sin, -cos)) // X wins
									:
										((cos, -sin, 0d), (sin, -cos, 0d)) // Z wins
									)
								;

								brushSide.texSAxis = sAxis;
								brushSide.texTAxis = tAxis;
								brushSide.texOffset = new Vector2d(xShift, yShift);
							}
							// xscale yscale
							if (!tokens.MoveNext())
								return result;
							double xScale = double.Parse(tokens.Current);
							if (xScale == 0)
								xScale = 1;
							if (!tokens.MoveNext())
								return result;
							double yScale = double.Parse(tokens.Current);
							if (yScale == 0)
								yScale = 1;
							// apply scale
							brushSide.texSAxis /= xScale;
							brushSide.texTAxis /= yScale;
							// we're done!
							brush.Add(brushSide);
						}
						// since we know the brush is complete, we can add it
						hasEntity.brushes.Add(brush);
					} else if (token == "}") {
						// "}" as 'key': end entity
						hasEntity = null;
					} else {
						hasKey = token;
					}
				} else if (token == "{") {
					// ignore tokens outside of entity start token
					hasEntity = new EntityParsed();
					result.Add(hasEntity);
				}
			}
			return result;
		}
	}
}
