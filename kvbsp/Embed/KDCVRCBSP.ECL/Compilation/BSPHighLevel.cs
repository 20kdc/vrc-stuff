using System.Collections.Generic;
using System.Linq;

namespace KDCVRCBSP.ECL {
	/// High-level compilation control functions.
	public static class BSPHighLevel {
		/// Maps from entities into Geo2.
		public static Geo2Map<M> Act1_MapIntoGeo2<M>(List<EntityParsed<M>> entities) where M : IBSPMaterial {
			var worldspawn = EntityParsed<M>.EnsureWorldspawn(entities);
			Geo2Context g2CtxWorld = new();
			List<EntityKeys> pointEntities = new();
			List<Geo2Map<M>.BrushEntity> brushEntities = new();
			List<(Geo2BrushInfo, List<EntityParsed<M>.BrushSide>)> worldBrushes = new();

			foreach (var entity in entities) {
				var classname = entity.pairs["classname"];
				List<(Geo2BrushInfo, List<EntityParsed<M>.BrushSide>)> targetBrushList = null;
				// simulate ericw-tools semantics
				Geo2BrushInfo brushInfo = new Geo2BrushInfo {
					allSurfaceFlags = 0,
					chopOrder = entity.pairs.GetInt("_chop_order", entity.pairs.GetBool("_chop", true) ? 0 : 1),
					cannotChop = (classname == "func_detail_fence" || classname == "func_detail_wall"),
					cannotBeChopped = entity.pairs.GetBool("_noclipfaces", false)
				};

				if (classname.StartsWith("func_detail"))
					brushInfo.AddSurfaceFlagSet(BSPSurfaceFlags.Detail);
				if (classname == "func_detail_illusionary")
					brushInfo.AddSurfaceFlagSet(BSPSurfaceFlags.MarkBrushIllusionary);

				if (classname == "func_group" || classname.StartsWith("func_detail") || classname == "worldspawn") {
					// group/world stuff
					targetBrushList = worldBrushes;
				} else if (entity.brushes.Count > 0) {
					// brush entity
					targetBrushList = new();
				}
				if (targetBrushList != null) {
					foreach (var brush in entity.brushes) {
						var brushInfoCopy = brushInfo;
						foreach (var face in brush)
							brushInfoCopy.allSurfaceFlags |= face.texture.SurfaceFlags;
						targetBrushList.Add((brushInfoCopy, brush));
					}
					// if target brush list is world, then this is subsumed
					// otherwise it isn't
					if (targetBrushList != worldBrushes)
						brushEntities.Add(new Geo2Map<M>.BrushEntity(new Geo2Context(), entity.pairs, targetBrushList));
				} else {
					pointEntities.Add(entity.pairs);
				}
			}

			Geo2Map<M>.BrushEntity g2World = new(g2CtxWorld, worldspawn.pairs, worldBrushes);
			return new Geo2Map<M>(g2World, pointEntities, brushEntities);
		}

		/// Compiles each brush entity.
		public static void Act2_CompileAll<M>(Geo2Map<M> map) where M : IBSPMaterial {
			var pointEntityLocations = map.pointEntities.Select(v => v.GetVector3d("origin", Vector3d.Zero)).ToList();
			Act2_CompilePartitionedEntity(map.worldspawn, pointEntityLocations);
			foreach (var ent in map.brushEntities)
				Act2_CompileEntity(ent);
		}

		/// Common 'compile entity' tasks:
		/// 1. Compile entity to (split, detail) face lists
		/// 2. Delete illusionary brushes
		public static void Act2_CompileEntityEarly<M>(Geo2Map<M>.BrushEntity entity, List<Convex3d<Geo2FaceInfo<M>>.Face> splitFaces, List<Convex3d<Geo2FaceInfo<M>>.Face> detailFaces) where M : IBSPMaterial {
			List<Convex3d<Geo2FaceInfo<M>>> choppers = new();
			// add all which are allowed to chop
			for (int cvxIdx = 0; cvxIdx < entity.brushes.Count; cvxIdx++) {
				var cvx = entity.brushes[cvxIdx];
				if (cvx.Item1.cannotChop)
					continue;
				choppers.Add(cvx.Item2);
			}
			// chop & extract faces
			for (int cvxIdx = 0; cvxIdx < entity.brushes.Count; cvxIdx++) {
				var cvx = entity.brushes[cvxIdx];
				IReadOnlyList<Convex3d<Geo2FaceInfo<M>>.Face> brushFaces = cvx.Item2.faces;
				if (!cvx.Item1.cannotBeChopped)
					brushFaces = cvx.Item2.ChopFaces(choppers, (f) => f.data.modSurfaceFlags);
				foreach (var f in brushFaces) {
					bool isDetail = (f.data.modSurfaceFlags & BSPSurfaceFlags.Detail) != 0;
					if (isDetail)
						detailFaces.Add(f);
					else
						splitFaces.Add(f);
				}
			}
			// remove illusionary
			entity.brushes.RemoveAll((b) => (b.Item1.allSurfaceFlags & BSPSurfaceFlags.MarkBrushIllusionary) != 0);
		}

		/// Compiles a 'regular' brush entity.
		public static void Act2_CompileEntity<M>(Geo2Map<M>.BrushEntity entity) where M : IBSPMaterial {
			var area = new Geo2Map<M>.Area();
			entity.areas.Add(area);
			Act2_CompileEntityEarly(entity, area.colliderFaces, area.colliderFaces);
		}

		/// Compiles worldspawn.
		public static void Act2_CompilePartitionedEntity<M>(Geo2Map<M>.BrushEntity entity, IReadOnlyList<Vector3d> pointEntities) where M : IBSPMaterial {
			if (true) {
				// nyi
				Act2_CompileEntity(entity);
				return;
			}
			/*
			List<Convex3d<(M, BrushUV)>.Face> splitFaces = new();
			List<Convex3d<(M, BrushUV)>.Face> detailFaces = new();
			Act2_CompileEntityEarly(entity, splitFaces, detailFaces);
			*/
		}

		/// Final compilation tasks
		/// 1. Build render faces
		/// 2. Delete collider faces
		public static void Act3_Postprocess<M>(Geo2Map<M> map) where M : IBSPMaterial {
			Act3_PostprocessEntity(map.worldspawn);
			foreach (var entity in map.brushEntities)
				Act3_PostprocessEntity(entity);
		}

		public static void Act3_PostprocessEntity<M>(Geo2Map<M>.BrushEntity entity) where M : IBSPMaterial {
			// build t-junction resolve list
			List<Vector3d> tJuncPoints = new();
			foreach (var area in entity.areas) {
				foreach (var face in area.colliderFaces) {
					if ((face.data.modSurfaceFlags & BSPSurfaceFlags.NoCreateTJunction) != 0)
						continue;
					foreach (var pt in face.winding)
						tJuncPoints.Add(pt);
				}
			}
			foreach (var area in entity.areas) {
				foreach (var face in area.colliderFaces) {
					var flags = face.data.modSurfaceFlags;
					if ((flags & BSPSurfaceFlags.DeleteAreaRenderFace) != 0)
						continue;
					// creating render face...
					IReadOnlyList<Vector3d> poly = face.winding;
					if ((flags & BSPSurfaceFlags.NoFixTJunction) == 0) {
						// t-junc fix
						List<Vector3d> poly2 = new(face.winding);
						GeomUtil.FixTJunctions(poly2, face.bounds, tJuncPoints, face.g2.broadphaseEpsilon, face.g2.distanceEpsilon);
						poly = poly2;
					}
					List<(Vector3d, Vector2d)> polyFinal = new();
					foreach (var vec in poly)
						polyFinal.Add((vec, face.data.texUV.MapUV(vec)));
					area.renderFaces.Add((face.data.material, polyFinal));
				}
				area.colliderFaces.RemoveAll((face) => {
					return (face.data.modSurfaceFlags & BSPSurfaceFlags.DeleteAreaColliderFace) != 0;
				});
			}
		}
	}
}
