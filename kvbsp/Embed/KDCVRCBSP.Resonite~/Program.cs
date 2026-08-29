using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using KDCVRCBSP.ECL;
using ResoniteLink;

namespace KDCVRCBSP.Resonite {
	public class Program {
		public static async Task Main(string[] args) {
			string bspFile = null;
			string wsUrl = null;
			double worldScale = 64;
			int i = 0;
			while (i < args.Length) {
				string arg = args[i++];
				if (arg.StartsWith("--")) {
					if (arg == "--worldScale") {
						worldScale = double.Parse(args[i++]);
					} else {
						throw new Exception("no idea what to do with arg " + arg);
					}
				} else if (bspFile == null)
					bspFile = arg;
				else if (wsUrl == null)
					wsUrl = arg;
				else
					throw new Exception("no idea what to do with arg " + arg);
			}
			if (wsUrl == null)
				throw new Exception("KDCVRCBSP.Resonite bspFile.bsp ws://localhost:1234 [--worldScale 123]");
			await MainBody(bspFile, wsUrl, worldScale);
		}

		public static async Task MainBody(string bspFile, string url, double worldScale) {
			byte[] bsp = File.ReadAllBytes(bspFile);
			var file = ECLBSPFile.Load(bsp);

			Console.Write("Connecting to Resonite @ " + url + " ...");
			LinkInterface resoLink = new();
			await resoLink.Connect(new Uri(url), CancellationToken.None);
			Console.WriteLine(" [OK]");

			// we should omit MessageID, ResoniteLink library will assign automatically
			SlotData infoAboutRoot = await resoLink.GetSlotData(new GetSlot {
				IncludeComponentData = false,
				SlotID = "Root",
				Depth = 0
			});
			foreach (var v in infoAboutRoot.Data.Children) {
				if (v.Name.Value == "TSKT_PrototypeSlot") {
					await resoLink.RemoveSlot(new RemoveSlot {
						SlotID = v.ID
					});
				}
			}

			// establish slot
			await resoLink.AddSlot(new AddSlot {
				Data = new Slot {
					ID = "TSKT_Root",
					Parent = new Reference {
						TargetID = "Root"
					},
					Name = new Field_string {
						Value = "TSKT_PrototypeSlot"
					}
				}
			});

			int entIdx = 0;
			foreach (var entity in file.entities) {
				if (entity.model != null) {
					// create entity slot
					var entSlotId = "TSKT_Ent_" + entIdx;
					await resoLink.AddSlot(new AddSlot {
						Data = new Slot {
							ID = entSlotId,
							Parent = new Reference {
								TargetID = "TSKT_Root"
							},
							Name = new Field_string {
								Value = "entity" + entIdx
							}
						}
					});

					int areaIdx = 0;
					foreach (var renderable in entity.model.renderables) {
						List<(ECLMesh.Vertex, ECLMesh.Vertex, ECLMesh.Vertex)> triangles = new();
						var mesh = renderable.Build();
						await Build(resoLink, entSlotId, entSlotId + "_Area" + areaIdx, mesh, worldScale);
						areaIdx++;
					}
				}
				entIdx++;
			}
		}

		public static async Task Build(LinkInterface resoLink, string targetSlot, string unique, ECLMesh mesh, double worldScale) {
			var asset = await BuildMesh(resoLink, mesh, worldScale);

			Dictionary<string, Member> staticMeshMembers = new();
			staticMeshMembers["URL"] = new Field_Uri {
				Value = asset.AssetURL
			};
			await resoLink.AddComponent(new AddComponent {
				ContainerSlotId = targetSlot,
				Data = new Component {
					ID = unique + "_staticMesh",
					ComponentType = "[FrooxEngine]FrooxEngine.StaticMesh",
					Members = staticMeshMembers
				}
			});

			Dictionary<string, Member> meshRendererMembers = new();
			meshRendererMembers["Mesh"] = new Reference {
				TargetID = unique + "_staticMesh"
			};
			await resoLink.AddComponent(new AddComponent {
				ContainerSlotId = targetSlot,
				Data = new Component {
					ID = unique + "_meshRenderer",
					ComponentType = "[FrooxEngine]FrooxEngine.MeshRenderer",
					Members = meshRendererMembers
				}
			});
		}

		public static Task<AssetData> BuildMesh(LinkInterface resoLink, ECLMesh mesh, double worldScale) {
			List<Vertex> resoVtx = new();
			List<Triangle> resoTris = new();

			foreach (ECLMesh.Vertex eclVtx in mesh.vertices) {
				resoVtx.Add(new Vertex {
					Position = new float3 {
						x = (float) (eclVtx.position.x / worldScale),
						y = (float) (eclVtx.position.y / worldScale),
						z = (float) (eclVtx.position.z / worldScale),
					}
				});
			}

			foreach ((int a, int b, int c) in mesh.triangles) {
				resoTris.Add(new Triangle {
					Vertex0Index = a,
					Vertex1Index = b,
					Vertex2Index = c,
				});
			}

			return resoLink.ImportMesh(new ImportMeshJSON {
				Vertices = resoVtx,
				Submeshes = new List<Submesh>(new Submesh[] {
					new TriangleSubmesh {
						Triangles = resoTris
					}
				})
			});
		}
	}
}
