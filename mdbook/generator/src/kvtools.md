# `vrc-stuff` Unity Packages And Their Contents

[The repository](https://github.com/20kdc/vrc-stuff/) contains a number of Unity packages.

Some of these (one, at present) can be installed via VPM; others must be manually installed by downloading the repository and using the 'from disk' option.

Where VPM versions are available, they may be installed via a VPM repo: `https://raw.githubusercontent.com/20kdc/vrc-stuff/refs/heads/main/vpm/index.json`

_As a general rule, frequently-changing/unstable packages will not receive a VPM release._

Naming scheme for display names is: `t` (for sorting) `20kdc` (because 20kdc) followed by:

* General: (no specific prefix, but name should sort before ` VRC `)
* VRC: ` VRC `
* World-related: ` World `
* A specific world: ` World:`

This helps with editor navigation.

## `kvworkarounds`

_`kvworkarounds` can be installed via the above VPM repo. As a set of workarounds that doesn't provide an API by itself, it is unlikely to break compatibility in a meaningful way._

`kvworkarounds` contains workarounds for the following issues:

1. When domain reloading is disabled, PlayerObjects typically cause ClientSim to stop functioning on second Play (<https://feedback.vrchat.com/persistence/p/clientsim-doesnt-work-with-player-objects-when-reload-domain-disabled>)
2. When domain reloading is disabled, NetworkCalling metadata may be 'stuck'

It does not presently patch or otherwise alter VRChat SDK code.

It is not a replacement for platform-specific fixes such as <https://github.com/BefuddledLabs/LinuxVRChatSDKPatch> -- `kvworkarounds` provides platform-independent workarounds for platform-independent issues which are outside of the scope of `LinuxVRChatSDKPatch`.

## `kvtools`

_`kvtools` can be installed via the above VPM repo. It isn't yet considered stable and might break compatibility._

It contains:

* `.udonjson` support for importing and using precompiled Udon programs
	* The `kvtools` set uses Unity JSON files with the extension `.udonjson` to represent `SerializedUdonProgramAsset`s for direct use in UdonBehaviour.
	* This is given as `.unity.json` in the dumper to avoid dumping files made by the dumper.
	* When writing out from within Unity, `EditorJsonUtility.ToJson` specifically _must_ be used, as otherwise file references are lost.
* Proxy assets
	* _Experimental!_ (I'm not sure if the asset 'importing' technique here has any odd effects.)
	* Proxy assets prevent Unity from breaking references when the source file is lost.
	* They are text files with extension `.proxyasset`. Their content is solely a relative path name (`..` may not be supported, so don't try it).

## `kvbsp`

_`kvbsp` can be installed via the above VPM repo. It isn't yet considered stable and might break compatibility._

See [the appropriate chapter](./bsp.md).

## `kvassets`

_`kvassets` is not intended for VPM release._

This package contains a set of 'common assets' for 20kdc worlds. It is a big melting-pot of unstable stuff.

## `kvresearch`

_`kvresearch` is not intended for VPM release._

Hyper-unstable research tooling package. Used to create the API JSON data used for the Udon extern documentation.

* `VRChat SDK/KDCVRCTools/Dataminer`
	* This menu option will create `Assets/datamine.txt`. The format of this is _really_ subject to change, more so than the rest of this, even; the format is solely meant to be an interface between it and `datamine2json.py`.
* `VRChat SDK/KDCVRCTools/Dump Odin`
	* This transforms `SerializedUdonPrograms` contents into `Library/KDCVRCDumpUdonJSON`, with `.odin.bin` and `.odin.json` files representing the extracted OdinSerializer contents.
	* _This should arguably be replaced with something more precisely targetted._
* `datamine2json.py`
	* This Python program expects `datamine.txt` to exist. It's really meant to be run on a Linux box and isn't particularly ashamed of this. It will create some files:
		* `api.json`: JSON dump of the known Udon API of VRChat. This includes type hierarchies.
		* `api_c.json`: Without formatting/indentation.
		* `api_x.json`: If pruning for submit is being done, this is a post-pruning but formatted/indented `api.json`.
			* Pruning isn't being done right now, especially as it now risks affecting the documentation.
		* `../../udon/kudon_apijson/src/api_c.json.xz`: `api_x.json` in small form for committing to Git.
		* `synctypes.md`: GFM-table-formatted table for import into the Network Calling Metadata table.
		* `statistics.md`: Statistics for easier diff review.
