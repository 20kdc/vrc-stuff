using System;
using System.Collections.Generic;

namespace KDCVRCBSP.ECL {
	/// Geo2Map represents a map in the BSP compiler.
	/// Once this object is created, the set of brushes is sealed.
	/// See BSPHighLevel.Act1_MapIntoGeo2.
	public sealed class Geo2Map<M> where M : IBSPMaterial {
		public readonly BrushEntity worldspawn;
		public readonly List<EntityKeys> pointEntities;
		/// Does not include worldspawn.
		public readonly List<BrushEntity> brushEntities;

		public Geo2Map(BrushEntity worldspawn, List<EntityKeys> pointEntities, List<BrushEntity> brushEntities) {
			this.worldspawn = worldspawn;
			this.pointEntities = pointEntities;
			this.brushEntities = brushEntities;
		}

		public class BrushEntity {
			public readonly Geo2Context g2;
			public readonly EntityKeys pairs;
			public readonly Vector3d origin;
			/// Bounds. Note that these bounds are *relative to the origin.*
			public readonly AABB3d bounds;
			/// Brushes and the entitykeys of their associated groups.
			public readonly List<(Geo2BrushInfo, Convex3d<Geo2FaceInfo<M>>)> brushes;

			/// This is where the areas go when either partitioned (worldspawn) or faux-partitioned (single area, for brush entities).
			public readonly List<Area> areas = new();

			/// This is set once partitioning happens if real partitioning is occurring.
			/// (If it isn't, then this is never set.)
			public BSPNode<Geo2FaceInfo<M>> root = null;

			/// Performs the bounds calculation, brush sorting, etc.
			public BrushEntity(Geo2Context g2, EntityKeys pairs, List<(Geo2BrushInfo, List<EntityParsed<M>.BrushSide>)> brushes) {
				this.g2 = g2;
				this.pairs = pairs;

				// Figure out origin
				Vector3d origin = pairs.GetVector3d("origin", Vector3d.Zero);
				AABB3d originAABB = new AABB3d {
					min = Vector3d.Zero,
					max = Vector3d.Zero
				};
				bool autoOrigin = pairs.GetBool("_kdcbsp_autoorigin", false);
				bool hasAnyOriginBrush = false;
				foreach (var brush in brushes) {
					// auto-origin mode works by treating *all* brushes as origin brushes
					if (!autoOrigin)
						if ((brush.Item1.allSurfaceFlags & BSPSurfaceFlags.MarkBrushOrigin) == 0)
							continue;
					// create working convex
					var translatedSides = new EntityParsed<M>.BrushSide[brush.Item2.Count];
					for (int i = 0; i < translatedSides.Length; i++)
						translatedSides[i] = brush.Item2[i].Translated(origin * -1);
					var convex = Convex3d<M>.FromBrush<M>(g2, translatedSides, (idx, src) => src.texture);
					if (!hasAnyOriginBrush) {
						originAABB = convex.bounds;
						hasAnyOriginBrush = true;
					} else {
						originAABB = originAABB.Join(convex.bounds);
					}
				}
				if (hasAnyOriginBrush)
					origin = (originAABB.min + originAABB.max) / 2;
				// Origin confirmed.
				this.origin = origin;
				AABB3d aabb3d = new AABB3d {
					min = Vector3d.Zero,
					max = Vector3d.Zero
				};
				List<(Geo2BrushInfo, Convex3d<Geo2FaceInfo<M>>)> brushesFinal = new();
				bool first = true;
				foreach (var brush in brushes) {
					var translatedSides = new EntityParsed<M>.BrushSide[brush.Item2.Count];
					for (int i = 0; i < translatedSides.Length; i++)
						translatedSides[i] = brush.Item2[i].Translated(origin * -1);
					var convex = Convex3d<Geo2FaceInfo<M>>.FromBrush<M>(g2, translatedSides, (idx, src) => {
						var modSurfaceFlags = src.texture.SurfaceFlags | brush.Item1.addSurfaceFlags;
						// mix in transfer flags from sides which are NOT this one
						for (int i = 0; i < translatedSides.Length; i++)
							if (i != idx)
								modSurfaceFlags |= translatedSides[i].texture.TransFlags;
						return new Geo2FaceInfo<M> {
							material = src.texture,
							texUV = src.texUV,
							modSurfaceFlags = modSurfaceFlags
						};
					});
					if (first) {
						aabb3d = convex.bounds;
						first = false;
					} else {
						aabb3d = aabb3d.Join(convex.bounds);
					}
					if ((brush.Item1.allSurfaceFlags & BSPSurfaceFlags.DeleteBrushAfterAABB) == 0)
						brushesFinal.Add((brush.Item1, convex));
				}
				this.bounds = aabb3d;
				this.brushes = brushesFinal;
			}
		}

		/// Areas. These may or may not contain leaves (null if not), but will contain faces if relevant.
		public sealed class Area {
			/// This contains the actual face list, for concave collision purposes.
			public readonly List<Convex3d<Geo2FaceInfo<M>>.Face> colliderFaces = new();
			/// This contains the render face list.
			public readonly List<Geo2RenderFace<M>> renderFaces = new();
			/// Leaves of this area. (Only exists if partitioned.)
			public List<BSPLeaf<Geo2FaceInfo<M>>> leaves = null;
		}
	}

	public struct Geo2RenderFace<M> {
		public M material;
		public List<(Vector3d, Vector2d)> polygon;
		public Plane3d plane;
		public AABB3d bounds;
	}

	/// Face information structure.
	public struct Geo2FaceInfo<M> {
		/// Modified surface flags.
		public BSPSurfaceFlags modSurfaceFlags;
		/// Material. IBSPMaterial IS NOT USED by this point; all information was safely extracted.
		public M material;
		/// UVs.
		public BrushUV texUV;
	}

	public struct Geo2BrushInfo {
		/// OR of all surface flags (with additional flags OR'd in if specified by context).
		/// Of these, only the ones specified as applying to brushes will be honoured.
		public BSPSurfaceFlags allSurfaceFlags;

		/// Surface flags being sent down from the brush entity.
		/// This a strict subset of allSurfaceFlags.
		public BSPSurfaceFlags addSurfaceFlags;

		/// This brush will never split world faces.
		public bool cannotChop;
		/// This brush will never be split by other brushes.
		public bool cannotBeChopped;
		/// Chop order.
		/// The meaning of this integer is described in the brush sorting code in BSPHighLevel.
		public int chopOrder;

		/// Makes sure to keep allSurfaceFlags and addSurfaceFlags coherent.
		public void AddSurfaceFlagSet(BSPSurfaceFlags flags) {
			allSurfaceFlags |= flags;
			addSurfaceFlags |= flags;
		}
	}
}
