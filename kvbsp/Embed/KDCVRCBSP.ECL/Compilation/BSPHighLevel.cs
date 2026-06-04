using System;
using System.Collections.Generic;
using System.Linq;

namespace KDCVRCBSP.ECL {
	/// High-level compilation control functions.
	public static class BSPHighLevel {
		/// Maps from entities into Geo2.
		public static Geo2Map<M> Act1_MapIntoGeo2<M>(List<EntityParsed<M>> entities) where M : IBSPMaterial {
			var worldspawn = EntityParsed<M>.EnsureWorldspawn(entities);
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
							brushInfoCopy.allSurfaceFlags |= face.texture.SurfaceFlags | face.texture.TransFlags;
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

			Geo2Map<M>.BrushEntity g2World = new(new Geo2Context(), worldspawn.pairs, worldBrushes);
			return new Geo2Map<M>(g2World, pointEntities, brushEntities);
		}

		/// Compiles each brush entity.
		public static void Act2_CompileAll<M>(Geo2Map<M> map, Predicate<EntityKeys> entityFills, bool chop, bool partition, bool allowLeaks, IBSPDiagnostics diag) where M : IBSPMaterial {
			if (partition) {
				List<(string, Vector3d)> pointEntityLocations = new();
				// Notably, point entity locations are relative to the entity being compiled.
				foreach (var entity in map.pointEntities) {
					if (!entityFills(entity))
						continue;
					if (entity.TryGetVector3d("origin", out var origin))
						pointEntityLocations.Add((entity["classname"], origin - map.worldspawn.origin));
				}
				Act2_CompilePartitionedEntity(map.worldspawn, chop, pointEntityLocations, allowLeaks, diag);
			} else {
				Act2_CompileEntity(map.worldspawn, chop);
			}
			foreach (var ent in map.brushEntities)
				Act2_CompileEntity(ent, chop);
		}

		/// Compile entity to (split, detail) face lists
		public static void Act2_CompileEntityEarly<M>(Geo2Map<M>.BrushEntity entity, bool chop, List<Convex3d<Geo2FaceInfo<M>>.Face> splitFaces, List<Convex3d<Geo2FaceInfo<M>>.Face> detailFaces) where M : IBSPMaterial {
			// Brush sorting (for chop order)
			// -- A SUMMARY OF CHOP ORDER (TRY NOT TO DELETE, YOU NEED THIS) --
			// There are two major concerns to consider with chop order.
			// Firstly, there is the ordering itself:
			//  ericw-tools describes chop order as if each brush is introduced one at a time, chopping previous brushes.
			//  However, the Convex3d.ChopFaces function works in reverse.
			//  This is due to it being written in a very parallelizable way (for future optimization).
			//  Each brush has its chopping handled individually and immutably to its own separate face list.
			//  The list of brushes that *can* chop is kept in one common immutable list.
			//  If the brush being processed cannot chop, it can't take priority, and it's not in the list.
			//  So the default state of affairs *must* be that a brush that isn't in the list is of lowest priority.
			//  It therefore follows that:
			//   In ericw-tools, the last brush is of highest priority (wins).
			//   In the embedded compiler, the last brush, or an omitted brush, is of lowest priority (loses).
			// Secondly, there is the matter of detail brushes.
			//  We absolutely do not want detail brushes EVER chopping world faces.
			//  If detail brushes chop world faces, there is now a hole in the BSP.
			//  Combine that hole with one bad BSP split, and now there are leaves escaping through that hole.
			//  But we still want detail brushes to chop each other and get chopped by world faces.
			//  Because of this, they still need to exist in one namespace; we can't just pretend they're separate brush entities.
			//  Ultimately, we sort the brushes accordingly, and then create two lists.
			//  One list has split brushes followed by detail brushes.
			//  The other list has split brushes only.
			//  Split brushes use the split brush only list, while detail brushes use the unified list.
			entity.brushes.Sort((a, b) => {
				bool aDetail = (a.Item1.allSurfaceFlags & BSPSurfaceFlags.Detail) != 0;
				bool bDetail = (b.Item1.allSurfaceFlags & BSPSurfaceFlags.Detail) != 0;
				if (aDetail != bDetail)
					return aDetail ? 1 : -1;
				// reverse chop order relative to ericw-tools
				if (a.Item1.chopOrder < b.Item1.chopOrder)
					return 1;
				if (a.Item1.chopOrder > b.Item1.chopOrder)
					return -1;
				return 0;
			});

			List<Convex3d<Geo2FaceInfo<M>>> choppersSplit = new();
			List<Convex3d<Geo2FaceInfo<M>>> choppersSplitAndDetail = new();
			// add all which are allowed to chop
			for (int cvxIdx = 0; cvxIdx < entity.brushes.Count; cvxIdx++) {
				var cvx = entity.brushes[cvxIdx];
				if (cvx.Item1.cannotChop)
					continue;
				// only add to choppersSplit if not detail so that detail won't XOR out splitters
				var brushIsDetail = (cvx.Item1.allSurfaceFlags & BSPSurfaceFlags.Detail) != 0;
				if (!brushIsDetail)
					choppersSplit.Add(cvx.Item2);
				choppersSplitAndDetail.Add(cvx.Item2);
			}
			// chop & extract faces
			for (int cvxIdx = 0; cvxIdx < entity.brushes.Count; cvxIdx++) {
				var cvx = entity.brushes[cvxIdx];
				IReadOnlyList<Convex3d<Geo2FaceInfo<M>>.Face> brushFaces = cvx.Item2.faces;
				if (chop && !cvx.Item1.cannotBeChopped) {
					var brushIsDetail = (cvx.Item1.allSurfaceFlags & BSPSurfaceFlags.Detail) != 0;
					var choppers = brushIsDetail ? choppersSplitAndDetail : choppersSplit;
					brushFaces = cvx.Item2.ChopFaces(choppers, (f) => f.data.modSurfaceFlags);
				}
				foreach (var f in brushFaces) {
					bool faceIsDetail = (f.data.modSurfaceFlags & BSPSurfaceFlags.Detail) != 0;
					if (faceIsDetail)
						detailFaces.Add(f);
					else
						splitFaces.Add(f);
				}
			}
		}

		/// Compiles a 'regular' brush entity.
		public static void Act2_CompileEntity<M>(Geo2Map<M>.BrushEntity entity, bool chop) where M : IBSPMaterial {
			var area = new Geo2Map<M>.Area();
			entity.areas.Add(area);
			Act2_CompileEntityEarly(entity, chop, area.colliderFaces, area.colliderFaces);
		}

		/// Compiles worldspawn.
		public static void Act2_CompilePartitionedEntity<M>(Geo2Map<M>.BrushEntity entity, bool chop, IReadOnlyList<(string, Vector3d)> pointEntities, bool allowLeaks, IBSPDiagnostics diag) where M : IBSPMaterial {
			List<Convex3d<Geo2FaceInfo<M>>.Face> splitFaces = new();
			List<Convex3d<Geo2FaceInfo<M>>.Face> detailFaces = new();
			Act2_CompileEntityEarly(entity, chop, splitFaces, detailFaces);
			// Console.WriteLine("Presorting face list...");
			BSPNode<Geo2FaceInfo<M>>.PresortFaceList(splitFaces);
			diag.Info($"Building tree ({splitFaces.Count} splitting faces...)");
			Predicate<Convex3d<Geo2FaceInfo<M>>.Face> faceIsSolid = (face) => {
				// not solid if detail or literally marked non-solid
				if ((face.data.modSurfaceFlags & BSPSurfaceFlags.Detail) != 0)
					return false;
				if ((face.data.modSurfaceFlags & BSPSurfaceFlags.BSPNonSolid) != 0)
					return false;
				return true;
			};
			var tree = BSPNode<Geo2FaceInfo<M>>.Build(entity.g2, splitFaces, detailFaces, Array.Empty<int>(), faceIsSolid);
			if (tree == null) {
				// something went wrong, fallback
				var area = new Geo2Map<M>.Area();
				entity.areas.Add(area);
				foreach (var face in splitFaces)
					area.colliderFaces.Add(face);
				foreach (var face in detailFaces)
					area.colliderFaces.Add(face);
			} else {
				List<BSPLeaf<Geo2FaceInfo<M>>> leaves = new();
				tree.AddLeaves(leaves);
				diag.Info($"Portalizing ({leaves.Count} leaves)...");
				BSPNode<Geo2FaceInfo<M>>.Portalize(leaves);
				diag.WriteDiagFileInfo(".prt", () => BSPNode<Geo2FaceInfo<M>>.MakePRT(leaves));
				diag.WriteDiagFileDebug(".leaves.obj", () => BSPNode<Geo2FaceInfo<M>>.MakeLeafOBJ(leaves));
				diag.WriteDiagFileDebug(".leafFaces.obj", () => {
					// dump a complete description of leaf faces in this OBJ
					List<(string, List<List<Vector3d>>)> objects = new();
					var index = 0;
					foreach (var leaf in leaves) {
						List<List<Vector3d>> leafObj = new();
						foreach (var face in leaf.faces)
							if (faceIsSolid(face))
								leafObj.Add(new List<Vector3d>(face.winding));
						objects.Add(("l" + index + "-faces", leafObj));
						index++;
					}
					return GeomUtil.DebugMakeOBJ(objects);
				});
				// split into areas
				HashSet<BSPLeaf<Geo2FaceInfo<M>>> seenLeaves = new();
				Queue<(string, Vector3d, BSPLeaf<Geo2FaceInfo<M>>)> areaStartQueue = new();
				// initialize queue with places where point entities are
				foreach (var pointEntityLoc in pointEntities) {
					var startLeaf = tree.Find(entity.g2, pointEntityLoc.Item2);
					areaStartQueue.Enqueue((pointEntityLoc.Item1, pointEntityLoc.Item2, startLeaf));
				}
				// consume queue start locations
				while (areaStartQueue.Count > 0) {
					var (areaStartName, areaStartPos, areaStartLeaf) = areaStartQueue.Dequeue();
					// if we already saw this leaf, that's that
					if (!seenLeaves.Add(areaStartLeaf))
						continue;
					List<BSPLeaf<Geo2FaceInfo<M>>> areaLeaves = new();
					areaStartLeaf.Explore(areaLeaves, seenLeaves, (portal) => {
						// check if portal is areaportal or non-traversable
						foreach (var face in portal.Item2.faces) {
							var flags = face.data.modSurfaceFlags;
							if ((flags & BSPSurfaceFlags.Areaportal) != 0) {
								// Register the opposing side.
								var portalPos = portal.Item2.windingBelow[0];
								if (portal.Item1 == portal.Item2.below) {
									areaStartQueue.Enqueue(("areaportal from " + areaStartName, portalPos, portal.Item2.above));
								} else {
									areaStartQueue.Enqueue(("areaportal from " + areaStartName, portalPos, portal.Item2.below));
								}
								return false;
							}
							if ((flags & BSPSurfaceFlags.BlockLeafTraversal) != 0)
								return false;
						}
						return true;
					});
					var area = new Geo2Map<M>.Area();
					HashSet<Convex3d<Geo2FaceInfo<M>>.Face> seenFaces = new();
					foreach (var leaf in areaLeaves) {
						if (leaf.convex.IsUnclosed && !allowLeaks) {
							diag.Warning("LEAK at " + areaStartName + " @ " + areaStartPos.ToString());
							// yeah, so, a leak has clearly occurred
							var route = leaf.Route(areaStartLeaf, (portal) => {
								// check if portal is areaportal or non-traversable
								foreach (var face in portal.faces) {
									var flags = face.data.modSurfaceFlags;
									if ((flags & BSPSurfaceFlags.Areaportal) != 0)
										return false;
									if ((flags & BSPSurfaceFlags.BlockLeafTraversal) != 0)
										return false;
								}
								return true;
							});
							if (route != null) {
								diag.WriteDiagFileWarning(".pts", () => {
									List<Vector3d> routeVecs = new();
									foreach (var leakLeaf in route)
										routeVecs.Add((leakLeaf.convex.bounds.min + leakLeaf.convex.bounds.max) / 2);
									routeVecs.Add(areaStartPos);
									return GeomUtil.DebugMakePTS(routeVecs);
								});
							} else {
								diag.Warning("LEAK IS UNROUTABLE");
							}
							allowLeaks = true;
						}
						foreach (var face in leaf.faces)
							if (seenFaces.Add(face))
								area.colliderFaces.Add(face);
					}
					entity.areas.Add(area);
				}
			}
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
					area.renderFaces.Add(new Geo2RenderFace<M> {
						material = face.data.material,
						polygon = polyFinal,
						plane = face.g2.FromPlaneIndex(face.planeIndex),
						bounds = face.bounds
					});
				}
				area.colliderFaces.RemoveAll((face) => {
					return (face.data.modSurfaceFlags & BSPSurfaceFlags.DeleteAreaColliderFace) != 0;
				});
			}
		}
	}
}
