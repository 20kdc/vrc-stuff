using System;

namespace KDCVRCBSP.ECL {
	/// These flags are used to control various stages of the compilation pipeline.
	/// This enum might be exposed to Unity, so it needs its own file.
	/// Note that you should absolutely read the ECL README to understand every detail of this.
	[Flags]
	public enum BSPSurfaceFlags {
		/// If this is present, the face can't be chopped.
		NoChopThis = 1,
		/// If this is present, the face can't chop other geometry.
		/// If not every face in the brush has this set, a special codepath has to be used.
		NoChopOthers = 2,
		/// If this is present, the face isn't solid. Solid faces delete leaves behind them, preventing navigation entirely.
		NonSolid = 4,
		/// If this is present, the face can't undergo vertex light slicing. (NYI)
		NoLightSlice = 8,
		/// If this is present, this face represents an areaportal.
		/// Faces with this flag should be non-solid, unchoppable, and still chop others.
		/// This flag has two effects:
		/// 1. It blocks leaf traversal.
		/// 2. When it blocks a leaf traversal, it adds the opposing portal side to the list of candidates for new areas.
		Areaportal = 16,
		/// If this is present, this face is expected to be deleted after BSP.
		DeleteFaceAfterBSP = 32,
		/// If this is present, the centre of the brush this face is on is the intended origin of the brush entity.
		/// The compiler checks for these brushes first in an entity, and finds the centre of the resulting AABB.
		Origin = 64,
		/// If this is present, this face does not contribute to the T-junction resolution pool.
		NoCreateTJunction = 128,
		/// If this is present, vertices will not be created to resolve T-junctions.
		NoFixTJunction = 256,
		/// If this is present, the brush to which this face is attached will be deleted after BSP.
		/// This creates the 'noclip brush'.
		DeleteBrushAfterBSP = 512,
		/// The AABB of the model is calculated immediately after creating its convexes.
		/// Once this occurs, delete this brush.
		/// All its faces will be lost.
		DeleteBrushAfterAABB = 1024,
	}
}
