using UnityEditor;
using VRC.Udon.Editor.ProgramSources;

namespace KDCVRCTools {
	// boilerplate that might be needed for Unity to act nice
	[CustomEditor(typeof(KDCJSONPrecompiledUdonAsset))]
	public class KDCJSONPrecompiledUdonAssetEditor : UdonProgramAssetEditor {}
}
