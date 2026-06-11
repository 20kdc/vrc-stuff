using UnityEngine;
using KDCVRCBSP.ECL;

namespace KDCVRCBSP {
	/**
	 * Entity parameterizer for info_player_start and similar TrenchBroom-only objects.
	 */
	public class KDCBSPDelmeEntityParameterizer : KDCBSPEntityParameterizer {
		public override void EntityParameterize(IKDCBSPImportContext ctx, ECLBSPFile.Entity entity, string uniqueName) {
			Object.DestroyImmediate(gameObject);
		}
	}
}
