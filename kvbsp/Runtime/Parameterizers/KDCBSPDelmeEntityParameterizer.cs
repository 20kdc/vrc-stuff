using UnityEngine;

namespace KDCVRCBSP {
	/**
	 * Entity parameterizer for info_player_start and similar TrenchBroom-only objects.
	 */
	public class KDCBSPDelmeEntityParameterizer : KDCBSPEntityParameterizer {
		public override void EntityParameterize(KDCBSPIntermediate bsp, ref KDCBSPIntermediate.Entity entity, string uniqueName) {
			Object.DestroyImmediate(gameObject);
		}
	}
}
