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

		/// Attempts to create a collision mesh from a brush.
		public static ECLMesh ToCollisionMesh(ECLBSPFile.Brush brush, double distanceEpsilon, double initialWindingSize) {
			return ToCollisionMesh(brush.ToPlanes(), distanceEpsilon, initialWindingSize);
		}

		/// Attempts to create a collision mesh from a brush's planes.
		public static ECLMesh ToCollisionMesh(Plane3d[] planes, double distanceEpsilon, double initialWindingSize) {
			return ToCollisionMesh(Convex3d<bool>.FromPlanes(planes, false, distanceEpsilon, initialWindingSize));
		}

		/// Creates a collision mesh from a Convex3d.
		public static ECLMesh ToCollisionMesh<D>(Convex3d<D> convex) {
			List<(Vector3d, IReadOnlyList<Vector3d>)> faces = new();
			foreach (var face in convex.faces)
				faces.Add((face.plane.normal, face.winding));
			return ToCollisionMesh(faces);
		}

		/// Creates a collision mesh from normals and windings.
		public static ECLMesh ToCollisionMesh(IReadOnlyList<(Vector3d, IReadOnlyList<Vector3d>)> faces) {
			int totalVertices = 0;
			int totalTriangles = 0;
			foreach (var (normal, winding) in faces) {
				if (winding.Count < 3)
					continue;
				totalVertices += winding.Count;
				totalTriangles += winding.Count - 2;
			}
			Vertex[] allVertices = new Vertex[totalVertices];
			(int, int, int)[] allTriangles = new (int, int, int)[totalTriangles];
			int vertexBase = 0;
			int triangleBase = 0;
			foreach (var (normal, winding) in faces) {
				for (int i = 0; i < winding.Count; i++)
					allVertices[vertexBase + i] = new Vertex {
						position = winding[i],
						normal = normal,
						colourR = 255,
						colourG = 255,
						colourB = 255,
						colourA = 255
					};
				for (int i = 0; i < winding.Count - 2; i++)
					allTriangles[triangleBase++] = (vertexBase, vertexBase + i + 1, vertexBase + i + 2);
				vertexBase += winding.Count;
			}
			return new ECLMesh(allVertices, allTriangles);
		}

		/// Merges all meshes. Useful for collision.
		public static ECLMesh Concatenate(IReadOnlyList<ECLMesh> sources) {
			int totalVertices = 0;
			int totalTriangles = 0;
			foreach (var mesh in sources) {
				totalVertices += mesh.vertices.Count;
				totalTriangles += mesh.triangles.Count;
			}
			Vertex[] allVertices = new Vertex[totalVertices];
			(int, int, int)[] allTriangles = new (int, int, int)[totalTriangles];
			int vertexBase = 0;
			int triangleBase = 0;
			foreach (var mesh in sources) {
				for (int i = 0; i < mesh.vertices.Count; i++)
					allVertices[vertexBase + i] = mesh.vertices[i];
				foreach (var tri in mesh.triangles)
					allTriangles[triangleBase++] = (tri.Item1 + vertexBase, tri.Item2 + vertexBase, tri.Item3 + vertexBase);
				vertexBase += mesh.vertices.Count;
			}
			return new ECLMesh(allVertices, allTriangles);
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
