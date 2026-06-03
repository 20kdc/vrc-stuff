using System;

namespace KDCVRCBSP {
	/**
	 * Runtime-code proxy of StaticEditorFlags.
	 * This is used so that i.e. components can refer to StaticEditorFlags.
	 */
	[Flags]
	public enum KDCBSPRuntimeStaticEditorFlags {
		ContributeGI = 1,
		OccluderStatic = 2,
		BatchingStatic = 4,
		NavigationStatic = 8,
		OccludeeStatic = 16,
		OffMeshLinkGeneration = 32,
		ReflectionProbeStatic = 64,
	}
}
