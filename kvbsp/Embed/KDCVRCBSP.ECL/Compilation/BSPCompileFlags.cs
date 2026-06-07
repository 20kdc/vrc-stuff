using System;

namespace KDCVRCBSP.ECL {
	/// These flags are used to control various stages of the compilation pipeline.
	/// This enum might be exposed to Unity, so it needs its own file.
	/// Note that you should absolutely read the ECL README to understand every detail of this.
	[Flags]
	public enum BSPCompileFlags {
		NoChop = 1,
		AllowLeaks = 2,
		NoPartition = 4,
		NoTJunc = 8,
		NoMeshOpt = 16,
		ExperimentalFlag = 0x10000,
	}
}
