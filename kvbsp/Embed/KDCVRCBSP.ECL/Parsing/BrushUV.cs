using System.Runtime.CompilerServices;

namespace KDCVRCBSP.ECL {
	/// Represents texture information.
	public struct BrushUV {
		/// Unnormalized (i.e. immediately usable) texture matrix.
		public Vector3d texSAxis, texTAxis;

		/// 'Rotation' reference value.
		public double rotation;

		/// Basis for texture matrix.
		public Vector2d texOffset;

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public Vector2d MapUV(Vector3d i) {
			return texOffset + new Vector2d((i * texSAxis).Sum, (i * texTAxis).Sum);
		}

		/// Translates the BrushUV in 3D space.
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public BrushUV Translated(Vector3d by) {
			return new BrushUV {
				texSAxis = texSAxis,
				texTAxis = texTAxis,
				texOffset = texOffset - new Vector2d((by * texSAxis).Sum, (by * texTAxis).Sum)
			};
		}
	}
}
