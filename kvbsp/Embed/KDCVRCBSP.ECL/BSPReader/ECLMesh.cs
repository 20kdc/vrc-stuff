using System;
using System.Collections.Generic;

namespace KDCVRCBSP.ECL {
	/// ECL mesh.
	public class ECLMesh {
		public readonly IReadOnlyList<Vertex> vertices;
		public readonly IReadOnlyList<(int, int, int)> triangles;

		public ECLMesh(IReadOnlyList<Vertex> vertices, IReadOnlyList<(int, int, int)> triangles) {
			this.vertices = vertices;
			this.triangles = triangles;
		}

		public struct Vertex {
			public Vector3d position;
			public Vector3d normal;
			public Vector2d uv;
			// q3bsp supports this, but should we? it adds extra VRAM load.
			// I guess have support for loading it and then just conveniently forget it during meshgen
			public byte colourR, colourG, colourB, colourA;

			public static Vertex BezierEval(Vertex a, Vertex b, Vertex c, double pos) {
				// Determine weights.
				double posInv = 1 - pos;
				// combined effects of all lines
				double weightA = posInv * posInv;
				double weightB = pos * posInv * 2;
				double weightC = pos * pos;
				// for i = 0, 10 do v = i / 10 print((v * (1 - v))) end
				// Add AB weight. Left side is vertex weight. Right side is line weight.
				// weightA += posInv * posInv;
				// weightB += pos * posInv;
				// Add BC weight.
				// weightB += posInv * pos;
				// weightC += pos * pos;

				// Determine 'trivial' weights.
				double trivialA = Math.Max(1 - (pos * 2), 0);
				double trivialB = 1 - (Math.Abs(pos - 0.5) * 2);
				double trivialC = Math.Max((pos * 2) - 1, 0);

				byte TrivialInterpolateColourByte(byte ba, byte bb, byte bc) {
					double r = (ba * trivialA) + (bb * trivialB) + (bc * trivialC);
					return (byte) Math.Min(Math.Max(Math.Round(r), 0), 255);
				}

				return new Vertex {
					position = (a.position * weightA) + (b.position * weightB) + (c.position * weightC),
					normal = ((a.normal * weightA) + (b.normal * weightB) + (c.normal * weightC)).Normalized,
					uv = (a.uv * trivialA) + (b.uv * trivialB) + (c.uv * trivialC),
					colourR = TrivialInterpolateColourByte(a.colourR, b.colourR, c.colourR),
					colourG = TrivialInterpolateColourByte(a.colourG, b.colourG, c.colourG),
					colourB = TrivialInterpolateColourByte(a.colourB, b.colourB, c.colourB),
					colourA = TrivialInterpolateColourByte(a.colourA, b.colourA, c.colourA),
				};
			}
		}
	}
}
