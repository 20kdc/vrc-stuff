using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;
using KDCVRCBSP;
using KDCVRCBSP.ECL;

/// A simple example of a KDCVRCBSP entity script.
/// Note the !COMPILER_UDONSHARP conditionals.
/// These are mandatory because UdonSharp won't like all the various non-Udon code.
public class MonsterDopefish : UdonSharpBehaviour
#if !COMPILER_UDONSHARP
	, IKDCBSPEntity
#endif
{
	public string text;
	public MonsterDopefish friend;

	void Start() {
		Debug.Log("monster_dopefish says: " + text);
	}

#if !COMPILER_UDONSHARP
	public void EntityCompile(IKDCBSPImportContext importContext, ECLBSPFile.Entity entity, string uniqueName) {
		// Beware: UdonSharp parameterizable entities won't compile brush entities by default!
		// If you need to implement that functionality, look at KDCBSPEntity.cs for reference.
		// ECLBSPFile.Entity is the parsed entity data.
		// You can get keys as strings directly, while there are dedicated 'get' functions for parsing various types.
		// Note that Quake coordinates are not Unity coordinates.
		// In order to transform them, use:
		//  * importContext.TransformPosition (scales)
		//  * importContext.TransformNormal (doesn't scale)
		//  * importContext.TransformPlane (for planes, scales)
		text = entity["message"];
		// All entities are instantiated before they are compiled.
		// This allows easily resolving cross-references.
		friend = importContext.FindByTargetname(entity["friend"]) as MonsterDopefish;
	}

	public void EntityPostProcess(IKDCBSPImportContext importContext) {
		// This function runs after all EntityCompile functions run.
		// There may be some reason you need to do something here.
	}

	// This property and the attributes function control FGD/ENT generation for map editors.

	public bool EntityFGDSolid => false;

	public void EntityFGDAttributes(KDCBSPEntityDescriptor descriptor) {

	}
#endif
}
