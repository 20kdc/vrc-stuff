# Version Control Practices

A complex VRChat project contains a large quantity of assets we'll call "quasi-imported".

Quasi-imported assets are assets which can be recompiled from source, but would lose their UUIDs if deleted, which means you have to ensure they're in place before even opening Unity.

These assets have a tendency of bloating the history and being generally _trouble,_ but you can't get rid of them, even if you might 'put them back later', because you'll risk breaking everything.

Finally, outside of Unity's general failings, VRChat in specific creates the `SerializedUdonPrograms` directory. This is mostly safe to delete, but causes diff churn.

In practice, the kinds of assets to worry about are:

* Category A: 'Package-relevant + Hard to compile'
	* Models
		* FBX files from Blender etc.
			* No good cross-platform solution here, esp. given addons/etc.
			* Conclusion: Drop in repository at site of use
	* BSP Data
		* TrenchBroom doesn't ship with a CLI export tool.
		* Realistically, we should be figuring out how to fold the BSP compiler invocation into KDCBSP.
			* This might actually be worse unless we can deal with TrenchBroom export somehow
		* Determinism is critical for lightmapping
		* Conclusion: Like with FBXs, we need to live with committing these to repository right now
	* Compiler output (uasm, udonjson)
		* Absolutely must be stored as-is in repository
		* Requires the whole RV64 compile setup. We **can't** make users depend on this.
		* Can affect devs too! 'pythonclassroom' uneditable at the beta site r/n because it depends on `/media/modus/External2/micropython/ports/kip32/build/firmware.udonjson` ^.^;
* Category B: 'UUIDs affect packages + Easy/automatic compile'
	* SerializedUdonPrograms
		* These are _named_ by the source program UUID (good!)
			* Example: Graph `2c4f4accf9f8ab90f85b18ed34a7222c` has serialized program `Assets/SerializedUdonPrograms/2c4f4accf9f8ab90f85b18ed34a7222c.asset` with UUID `db947642fbcf1517c9bd40f2fb7c1c70`
		* Within a single project, if deleted, the file is regenerated _and usually somehow manages to have the same UUID!_
			* I believe there's some mechanism going on in Unity to try and 'recover' typical lost-UUID situations, but I'm not sure.
			* _**Sadly, between projects, it regenerates all the UUIDs :(**_
				* Workaround found.
		* There are a lot of very good reasons VRChat made it work this way, but the lack of GUID pinning 'long-term' is a pain.
		* Sad: VRChat 'signature' system causes these to be **highly volatile.**
			* These can't safely enter VCS unless `VRCFastCrypto.GenerateSigningKey` 'happens' to generate the same key every time.
		* Conclusion: Removable. `kvtools` now provides deterministic GUIDs to avoid diff churn
* Category C: 'Only affects Worlds'
	* Global Illumination Bake Results
		* These 'sort of' solve themselves 'to some extent'.
		* The mobile planar map shaders are particularly causing dependencies on reflection probes.
			* In the long-term, this should be solved using on-build behaviours on relevant reflection probes. But proxy assets will help in the meantime.
			* An alternative solution is to outright give these dedicated 'stable cubemaps'.
* Upstream VPM packages
	* Solution: Dedicated internal repository for 'hollow' avatar and worlds projects?
		* This can be used for dealing with other problems also

## Proxy Assets

Proxy assets are a component of `kvtools`, and prevent Unity from breaking references.

They are text files with extension `.proxyasset`. Their content is solely a relative path name (`..` may not be supported, so don't try it).

An example of their content might be:

`pythonclassroom/ReflectionProbe-7.exr`

They seem to work just fine from within packages (though _why_ this works is unclear).

## Deterministic SerializedUdonProgramAsset GUIDs

This is a `kvtools` feature. It adds `Re-compile + Deterministic GUIDs` next to the `Re-compile All Program Sources` button.

Deterministic GUIDs ensure that SerializedUdonProgramAssets don't cause diff churn by forcing their GUIDs to be derived from their parent source code.

<!--
## Solving the `serializedUdonProgramAsset` diff churn problem?

* Obvious brute-force way: Keep them (and hey, why not i.e. bake results?) in a private VCS
	* Nuke history every so often
* Less obvious way: Consistently generate the same UUID every time by tricking Unity
* Even less obvious way: Turn up Harmony patching to 11 to make SerializedUdonProgramAsset references _actually_ lazy-load references, which can be used to ensure the UUID exists
* Maybe practical idk way: In `kvworkarounds`, on initial project load, find all unreferenced SerializedUdonProgramAssets and guarantee they exist
	* How to find unreferenced SUPAs?
* 'VRChat probably have it right' way: Make a dedicated per-package SerializedUdonProgramAsset directory and simply eat the diff churn there
	* Forcing all package devs to 'opt out' of secure signature checking may be a step too far
* Thought: Any solution that still causes diff churn for an unmodified ProTV is a stupid solution
	* This rules out any solution that requires opt-in from package devs
	* The brute-force way wins by default

* Menu option which rewrites all SerializedUdonProgramAsset asset metadata to have deterministic UUIDs.
	* XOR some of the original UUID bytes; either inverting them or by some unique mask.
	* Sketchy, but not that crazy...!
	* Solely relies on things we 'know for sure' about how VRCSDK performs lookups.
	* Tie into also running `UdonEditorManager.RecompileAllProgramSources` and put next to it in the menu.
-->
