using System;
using System.Collections.Generic;

namespace KDCVRCBSP.ECL {
	/// For the host of the BSP compiler.
	public interface IBSPDiagnostics {
		public void Info(string text);
		public void Warning(string text);
		public bool DebugEnabled { get; }
		public BSPCompileFlags CompileFlags { get; }
		public void WriteDiagFileDebug(string filename, Func<List<string>> text);
		public void WriteDiagFileInfo(string filename, Func<List<string>> text);
		public void WriteDiagFileWarning(string filename, Func<List<string>> text);
	}
}
