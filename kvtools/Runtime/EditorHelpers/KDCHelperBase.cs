using UnityEngine;
using VRC.SDKBase;

namespace KDCVRCTools {
	/**
	 * KDCHelperBase is the base class for MonoBehaviours which provide 'editor automation'.
	 * OnPreprocess is intended to be the central 'things happen here' function.
	 */
	public abstract class KDCHelperBase : MonoBehaviour, IEditorOnly, IPreprocessCallbackBehaviour {
		public virtual void OnValidate() {
			hideFlags = HideFlags.DontSaveInBuild;
			// OnPreprocess();
		}

		public virtual void Reset() {
			hideFlags = HideFlags.DontSaveInBuild;
		}

		public virtual void Awake() {
			OnPreprocess();
		}

		public int PreprocessOrder => 0;

		public virtual bool OnPreprocess() {
			return true;
		}
	}
}
