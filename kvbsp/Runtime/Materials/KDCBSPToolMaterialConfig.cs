using System;
using System.Text;
using UnityEngine;
using KDCVRCBSP.ECL;

namespace KDCVRCBSP {
	/// Special path for tool materials.
	/// These are **always** invisible.
	[CreateAssetMenu(menuName = "KDCVRCTools/KDCBSP Tool Material Config", fileName = "materialConfig")]
	public class KDCBSPToolMaterialConfig : KDCBSPAbstractMaterialConfig {

		[Tooltip(".wal_json file contents")]
		[Multiline(16)]
		[SerializeField]
		public string ericwToolsWALJson;

		[Tooltip("Quake 3 / NetRadiant / q3map2 shader code.")]
		[Multiline(16)]
		[SerializeField]
		public string quake3Shader;

		public override GameObject BuildVisualObject(IKDCBSPImportContext ctx, string materialName, string meshAssetName, ECLMesh data, Func<Vector2, Mesh> buildDefaultMesh, GameObject visualsGO, KDCBSPBrushEntitySettings brushEntitySettings) {
			return null;
		}

		public override float GetCollisionConvexPriority(Vector3 normal) {
			return 100 + normal.y;
		}

		public override string PAKGetWALJSON(string materialPath, string discoveryPath) {
			return ericwToolsWALJson;
		}

		public override string PAKGetQ3Shader(string materialPath, string discoveryPath) {
			return quake3Shader;
		}
	}
}
