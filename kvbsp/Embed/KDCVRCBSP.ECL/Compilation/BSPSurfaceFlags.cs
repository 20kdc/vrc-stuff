using System;

namespace KDCVRCBSP.ECL {
	/// These flags are used to control various stages of the compilation pipeline.
	/// This enum might be exposed to Unity, so it needs its own file.
	/// Note that you should absolutely read the ECL README to understand every detail of this.
	[Flags]
	public enum BSPSurfaceFlags {

		// -- Preprocessing (**000000) --
		/// If this is present, the centre of the brush this face is on is the intended origin of the brush entity.
		/// The compiler checks for these brushes first in an entity, and finds the centre of the resulting AABB.
		/// Don't forget to set this as DeleteBrushAfterAABB.
		MarkBrushOrigin = 0x01000000,
		/// A surface of this kind existing on a brush forcibly sets the whole brush as illusionary without a brush entity.
		MarkBrushIllusionary = 0x02000000,
		/// Delete this brush after the AABB of the entity is confirmed.
		/// All brush faces will be lost.
		DeleteBrushAfterAABB = 0x10000000,

		// -- Chop (00**0000) --
		/// If this is present, the face can't be chopped.
		/// (As something of a hack, this is also used to control vertex light slicing in the CMF builder.)
		NoChopThis = 0x00010000,
		/// If this is present, the face can't chop other geometry.
		/// If not every face in the brush has this set, a special codepath has to be used.
		NoChopOthers = 0x00020000,
		/// This flag must exist in the TransFlags field unless you are EXTREMELY careful.
		/// It causes the face to not split the BSP, which can violate various assumptions.
		Detail = 0x00040000,

		// -- Partitioning (0000**00) --
		/// If this is present, the face isn't solid to the BSP. Solid faces delete leaves behind them, preventing navigation entirely.
		NoDeleteLeaves = 0x00000100,
		/// If this is present, this face blocks traversal between leaves.
		/// This could, in theory, be useful for creating a double-sided object that you want to block.
		BlockLeafTraversal = 0x00000200,
		/// If this is present, this face represents an areaportal.
		/// Faces with this flag should be non-solid, unchoppable, and still chop others.
		/// This flag has two effects:
		/// 1. It blocks leaf traversal. (Areaportal takes priority over BlockLeafTraversal.)
		/// 2. When it blocks a leaf traversal, it adds the opposing portal side to the list of candidates for new areas.
		Areaportal = 0x00000400,

		// -- Post-processing (000000**) --
		/// If this is present, the non-render version of this face should be deleted.
		/// This should only be used in specialized circumstances, and may never be used in practice.
		DeleteAreaColliderFace = 0x00000001,
		/// This material should not create render faces.
		/// Note that some environments (Narbacular Drop) do not differentiate collider/render faces.
		/// In this event, deleting render faces will always delete the collision associated with them.
		DeleteAreaRenderFace = 0x00000002,
		/// If this is present, the render face does not contribute to the T-junction resolution pool.
		NoCreateTJunction = 0x00000010,
		/// If this is present, the render face will not be created to resolve T-junctions.
		NoFixTJunction = 0x00000020
	}
}
