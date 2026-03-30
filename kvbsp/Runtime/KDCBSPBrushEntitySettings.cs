using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace KDCVRCBSP {
	/**
	 * Brush entity compile settings.
	 */
	[System.Serializable]
	public class KDCBSPBrushEntitySettings {
		[Tooltip("Controls if/how collision is generated.")]
		[SerializeField]
		public CollisionMode collision;
		[Tooltip("Controls the Is Trigger flag on collision.")]
		[SerializeField]
		public bool collisionIsTrigger;

		public void ApplyColliderSettings(Collider collider) {
			collider.isTrigger = collisionIsTrigger;
		}

		public enum CollisionMode {
			/// Collisions are handled using one convex mesh collider per brush.
			/// This generates a lot of Mesh objects, but is great for physics broadphase and produces dependable collisions.
			/// TLDR: Fast and good. If you want collisions, use this whenever possible.
			ConvexBrushes,
			/// Collisions are handled using a concave (triangle-soup) mesh collider attached to the root.
			/// This collider includes faces which the BSP collider included, but which are nodraw. (`common/noclip` is specifically removed by name.)
			/// This collision mode is not recommended for world geometry due to being clip-prone by nature and essentially ruining physics broadphase.
			/// However, it may be of use in props, where interactability is the bigger concern.
			ConcaveRoot,
			/// Single convex mesh made from all brushes on root.
			SingleConvexRoot,
			/// Collisions are not generated.
			None
		}
	}
}
