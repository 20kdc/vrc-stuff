using UnityEngine;
using VRC.SDKBase;

namespace KDCVRCTools {
	/**
	 * KDCHelperBase is the base class for MonoBehaviours which provide 'editor automation'.
	 * OnPreprocess is intended to be the central 'things happen here' function.
	 */
	public abstract class KDCHelperBase : MonoBehaviour, IEditorOnly, IPreprocessCallbackBehaviour {
		void OnValidate() {
			hideFlags = HideFlags.DontSaveInBuild;
			// OnPreprocess();
		}

		void Reset() {
			hideFlags = HideFlags.DontSaveInBuild;
		}

		void Awake() {
			OnPreprocess();
		}

		public int PreprocessOrder => 0;

		public virtual bool OnPreprocess() {
			return true;
		}
	}
}
