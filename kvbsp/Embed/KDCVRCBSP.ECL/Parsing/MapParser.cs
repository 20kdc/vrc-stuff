using System;
using System.Collections.Generic;

namespace KDCVRCBSP.ECL {
	/// Tokenizes and parses the given .map data or entity lump.
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
	public class MapParser {
		private static char[] whitespace = new char[33];
		private static char[] quoteOrEscape = new char[] {'\"', '\\'};

		static MapParser() {
			for (int i = 0; i < 33; i++)
				whitespace[i] = (char) i;
		}

		public readonly string lump;
		public int position = 0;

		/// Current token.
		public string current = "";

		/// Pushback flag.
		/// If enabled, NextToken will simply reset the flag and return true.
		public bool pushback = false;

		public MapParser(string lump) {
			this.lump = lump;
		}

		/// Returns the current line number.
		public int LineNumber {
			get {
				int ln = 1;
				for (int i = 0; i < position; i++)
					if (lump[i] == '\n')
						ln++;
				return ln;
			}
		}

		/// Skips whitespace and comments.
		public void SkipWhitespace() {
			while (position < lump.Length) {
				if (lump[position] > 32) {
					int remaining = lump.Length - position;
					char ch0 = lump[position];
					char ch1 = (remaining >= 2) ? lump[position + 1] : (char) 0;
					if (ch0 == ';' || (ch0 == '/' && ch1 == '/')) {
						// Line comment.
						int endPoint = lump.IndexOf('\n', position);
						if (endPoint == -1) {
							position = lump.Length;
							return;
						}
						position = endPoint + 1;
						continue;
					} else {
						// No, this is definitely a token.
						break;
					}
				}
				position++;
			}
		}

		/// Reads the next token from the stream.
		public bool NextToken() {
			if (pushback) {
				pushback = false;
				return true;
			}
			// This handles skipping all line comments/etc.
			// After this we either have a token or no tokens remain.
			SkipWhitespace();
			if (position >= lump.Length)
				return false;
			// What happens now depends on the first character.
			char ch0 = lump[position];
			if (ch0 == '\"') {
				// Quoted string.
				position++;
				current = "";
				// Breaking from this look should only happen if early EOF is hit.
				while (position < lump.Length) {
					// 'regular content' phase
					{
						int nextPtr = lump.IndexOfAny(quoteOrEscape, position);
						if (nextPtr == -1)
							break;
						// complete all regular chars up to nextPtr
						current += lump.Substring(position, nextPtr - position);
						position = nextPtr;
					}
					// special phase
					char nxp = lump[position];
					if (nxp == '\\') {
						if (position >= (lump.Length - 1))
							break;
						char escape = lump[position + 1];
						bool escapeAccepted = false;
						if (escape == '\\' || escape == '\"') {
							// verbatim
							escapeAccepted = true;
						}
						if (escapeAccepted) {
							position += 2;
							current += escape;
						} else {
							// Narbacular Drop fix: Pretend unhandled escapes aren't escapes
							current += "\\";
							position++;
						}
					} else {
						// if (nxp == '"')
						position++;
						return true;
					}
				}
				// Early EOF
				current += lump.Substring(position);
				return true;
			} else {
				// Unquoted.
				int endPoint = lump.IndexOfAny(whitespace, position);
				if (endPoint == -1) {
					current = lump.Substring(position);
					position = lump.Length;
				} else {
					current = lump.Substring(position, endPoint - position);
					position = endPoint;
				}
				return true;
			}
		}

		/// Parses the map.
		public List<EntityParsed<M>> ParseMap<M>(Func<string, M> mapTextureToMaterial) {
			List<EntityParsed<M>> result = new();
			EntityParsed<M> hasEntity = null;
			string hasKey = null;
			Vector3d[] pointBuf = new Vector3d[3];
			double[] stBuf = new double[8];
			while (NextToken()) {
				string token = current;
				if (hasEntity != null) {
					if (hasKey != null) {
						// value takes precedence to allow for "{" and "}" as values
						hasEntity.pairs.Add((hasKey, token));
						hasKey = null;
					} else if (token == "{") {
						// "{" as 'key': start brush
						List<EntityParsed<M>.BrushSide> brush = new();
						// expected side forms:
						// ID  : ( x y z ) ( x y z ) ( x y z ) texture xshift yshift rotation xscale yscale
						// V220: ( x y z ) ( x y z ) ( x y z ) texture [ x y z w ] [ x y z w ] rotation xscale yscale
						while (true) {
							// skip until we reach "(" (next brushside start) or "}" (end of brush)
							while (true) {
								// if we hit early EOF, we abandon this brush.
								// the entity will remain in the result
								if (!NextToken())
									return result;
								token = current;
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
									NextToken(); // (

								if (!NextToken())
									return result;
								double x1 = double.Parse(current);
								if (!NextToken())
									return result;
								double y1 = double.Parse(current);
								if (!NextToken())
									return result;
								double z1 = double.Parse(current);
								pointBuf[i] = new Vector3d(x1, y1, z1);

								if (!NextToken()) // )
									return result;
							}
							// texture
							if (!NextToken())
								return result;
							string texture = current;
							// Gets filled in with texture info
							var brushSide = new EntityParsed<M>.BrushSide {
								vertexA = pointBuf[0],
								vertexB = pointBuf[1],
								vertexC = pointBuf[2],
								texture = mapTextureToMaterial(texture)
							};
							// ID  : xshift yshift rotation xscale yscale
							// V220: [ x y z w ] [ x y z w ] rotation xscale yscale
							// notably:
							//  * Q2 adds extra metadata we don't care about.
							//  * rotation is basically useless in V220 (editor-only). but xscale/yscale isn't!
							//  * everything's arranged in such a way that the 'smart move' is to apply xscale/yscale always
							if (!NextToken())
								return result;
							if (current == "[") {
								// V220: [ x y z w ] [ x y z w ] rotation
								for (int q = 0; q < 8; q++) {
									if (!NextToken())
										return result;
									stBuf[q] = double.Parse(current);
									if (q == 3) {
										if (!NextToken()) // ]
											return result;
										if (current != "]")
											throw new Exception("Expected ], got " + current);
										if (!NextToken()) // [
											return result;
										if (current != "[")
											throw new Exception("Expected [, got " + current);
									}
								}
								if (!NextToken()) // ]
									return result;
								if (current != "]")
									throw new Exception("Expected ], got " + current);
								// remaining: rotation
								// we skip rotation
								if (!NextToken())
									return result;
								double.Parse(current);
								brushSide.texUV.texSAxis = new Vector3d(stBuf[0], stBuf[1], stBuf[2]);
								brushSide.texUV.texTAxis = new Vector3d(stBuf[4], stBuf[5], stBuf[6]);
								brushSide.texUV.texOffset = new Vector2d(stBuf[3], stBuf[7]);
							} else {
								// ** DO NOT RELY ON THIS CODE. **
								// (It needs some tweaks that probably aren't coming)

								// ID: xshift yshift rotation
								// initial MoveNext does not happen because current token is already xshift
								double xShift = double.Parse(current);
								if (!NextToken())
									return result;
								double yShift = double.Parse(current);
								if (!NextToken())
									return result;
								double rotation = double.Parse(current);

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

								brushSide.texUV.texSAxis = sAxis;
								brushSide.texUV.texTAxis = tAxis;
								brushSide.texUV.texOffset = new Vector2d(xShift, yShift);
							}
							// xscale yscale
							if (!NextToken())
								return result;
							double xScale = double.Parse(current);
							if (xScale == 0)
								xScale = 1;
							if (!NextToken())
								return result;
							double yScale = double.Parse(current);
							if (yScale == 0)
								yScale = 1;
							// apply scale
							brushSide.texUV.texSAxis /= xScale;
							brushSide.texUV.texTAxis /= yScale;
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
					hasEntity = new EntityParsed<M>();
					result.Add(hasEntity);
				}
			}
			return result;
		}

		/// Tokenizes to a string list.
		public static List<string> Tokenize(string entityLump) {
			MapParser parser = new MapParser(entityLump);
			List<string> tokens = new();
			while (parser.NextToken())
				tokens.Add(parser.current);
			return tokens;
		}

		/// Tokenizes and parses the given map.
		public static List<EntityParsed<M>> Parse<M>(string map, Func<string, M> mapTextureToMaterial) {
			MapParser parser = new(map);
			return parser.ParseMap<M>(mapTextureToMaterial);
		}
	}
}
