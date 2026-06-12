using UnityEngine;
using KDCVRCBSP.ECL;

namespace KDCVRCBSP {
	/**
	 * Entity parameterizer for info_player_start and similar TrenchBroom-only objects.
	 */
	public class KDCBSPDelmeEntity : KDCBSPEntity {
		public override void EntityCompile(IKDCBSPImportContext importContext, ECLBSPFile.Entity entity, string uniqueName) {
			Object.DestroyImmediate(gameObject);
		}
	}
}
