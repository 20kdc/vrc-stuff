# Version Control Practices

A complex VRChat project contains a large quantity of assets we'll call "quasi-imported".

Quasi-imported assets are assets which can be recompiled from source, but would lose their UUIDs if deleted, which means you have to ensure they're in place before even opening Unity.

These assets have a tendency of bloating the history and being generally _trouble,_ but you can't get rid of them, even if you might 'put them back later', because you'll risk breaking everything.

Finally, outside of Unity's general failings, VRChat in specific creates the `SerializedUdonPrograms` directory. This is mostly safe to delete, but causes diff churn.

In practice, the kinds of assets to worry about are:

* Models
	* FBX files from Blender etc.
		* No good cross-platform solution here, esp. given addons/etc.
* BSP Data
	* TrenchBroom doesn't ship with a CLI export tool.
	* Realistically, we should be figuring out how to fold the BSP compiler invocation into KVTools.
		* But this might actually be worse unless we can deal with TrenchBroom export somehow
	* Determinism is critical for lightmapping
	* Like with FBXs, we need to live with committing these to repository (but we don't _want_ to) right now
* Global Illumination Bake Results
	* These 'sort of' solve themselves 'to some extent'.
	* The mobile planar map shaders are particularly causing dependencies on reflection probes.
		* In the long-term, this should be solved using on-build behaviours on relevant reflection probes. But proxy assets will help in the meantime.
		* An alternative solution is to outright give these dedicated 'stable cubemaps'.
* Compiler output (uasm, udonjson)
	* Absolutely must be stored as-is in repository
* SerializedUdonPrograms
	* These are _named_ by the source program UUID (good!)
		* Example: Graph `2c4f4accf9f8ab90f85b18ed34a7222c` has serialized program `Assets/SerializedUdonPrograms/2c4f4accf9f8ab90f85b18ed34a7222c.asset` with UUID `db947642fbcf1517c9bd40f2fb7c1c70`
	* If an 'accident' happens, the file is regenerated _**and somehow manages to have the same UUID (really good!)**_
		* I believe there's some mechanism going on in Unity to try and 'recover' typical lost-UUID situations by basing the original UUID on the file path, but I'm not sure.
		* If so, what does this mechanism do between projects?
	* There are a lot of very good reasons VRChat made it work this way
	* Sad: VRChat 'signature' system causes these to be **highly volatile.**
		* These can't safely enter VCS unless `VRCFastCrypto.GenerateSigningKey` 'happens' to generate the same key every time.
* Upstream VPM packages
	* Solution: Dedicated internal repository for 'hollow' avatar and worlds projects

**TODO: This is all draft stuff r/n**

## Proxy Assets

Proxy assets are a component of `kvtools`, and prevent Unity from breaking references.

They are text files with extension `.proxyasset`. Their content is solely a relative path name (`..` may not be supported, so don't try it).

An example of their content might be:

`pythonclassroom/ReflectionProbe-7.exr`

They seem to work just fine from within packages (though _why_ this works is unclear).
