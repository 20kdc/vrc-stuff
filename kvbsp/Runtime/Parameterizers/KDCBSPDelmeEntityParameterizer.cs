using UnityEngine;
using KDCVRCBSP.ECL;

namespace KDCVRCBSP {
	/**
	 * Entity parameterizer for info_player_start and similar TrenchBroom-only objects.
	 */
	public class KDCBSPDelmeEntityParameterizer : KDCBSPEntityParameterizer {
		public override void EntityParameterize(ECLBSPFile bsp, ECLBSPFile.Entity entity, string uniqueName, float worldScale) {
			Object.DestroyImmediate(gameObject);
		}
	}
}
