# `kvtools`

`kvtools` is a Unity package in this repository, at <https://github.com/20kdc/vrc-stuff/tree/main/kvtools>.

It contains:

* Bug workarounds.
* Datamining infrastructure used to create and maintain this documentation.
* `.udonjson` support for importing and using precompiled Udon programs
* Tools to dump compiled Udon programs in your Worlds project
* Quake 2 BSP import

## Bug Workarounds

`kvtools` contains workarounds for some bugs:

1. When domain reloading is disabled, PlayerObjects typically cause ClientSim to stop functioning on second Play (<https://feedback.vrchat.com/persistence/p/clientsim-doesnt-work-with-player-objects-when-reload-domain-disabled>)
2. When domain reloading is disabled, NetworkCalling metadata may be 'stuck'

## `.udonjson`

The `kvtools` set uses Unity JSON files with the extension `.udonjson` to represent `SerializedUdonProgramAsset`s for direct use in UdonBehaviour.

This is given as `.unity.json` in the dumper to avoid dumping files made by the dumper.

When writing out from within Unity, `EditorJsonUtility.ToJson` specifically _must_ be used, as otherwise file references are lost.

## `VRChat SDK/KDCVRCTools/Dataminer`

This menu option will create `Assets/datamine.txt`. The format of this is _really_ subject to change, more so than the rest of this, even; the format is solely meant to be an interface between it and `datamine2json.py`.

## `datamine2json.py`

This Python program expects `datamine.txt` to exist.

It's really meant to be run on a Linux box and isn't particularly ashamed of this.

It will create some files:

* `api.json`: JSON dump of the known Udon API of VRChat. This includes type hierarchies.
* `api_c.json`: Without formatting/indentation.
* `api_x.json`: If pruning for submit is being done, this is a post-pruning but formatted/indented `api.json`.
	* Pruning isn't being done right now, especially as it now risks affecting the documentation.
* `../../udon/kudon_apijson/src/api_c.json.xz`: `api_x.json` in small form for committing to Git.
* `synctypes.md`: GFM-table-formatted table for import into the Network Calling Metadata table.
* `statistics.md`: Statistics for easier diff review.

## `VRChat SDK/KDCVRCTools/Dump Odin`

This transforms `SerializedUdonPrograms` contents into `Library/KDCVRCDumpUdonJSON`.

This contains `.odin.bin` and `.odin.json` files representing the extracted OdinSerializer contents.

_This should arguably be replaced with something more precisely targetted._

## Quake 2 BSP Import

The Quake 2 BSP import feature allows creating static world geometry with, i.e. TrenchBroom and a more recent `ericw-tools` (such as one of the 2.0.0 alpha releases from <https://github.com/ericwa/ericw-tools/releases>).

The basic idea is that, first, you symlink:
* `.TrenchBroom/games/KVToolsTB` to the `KVToolsTB` directory of the repository
* `qroot/baseq2` (where `qroot` is a 'global workspace') to the `baseq2` directory of the repository
* Each individual project is a 'mod' you symlink from `qroot`

Note the `KVToolsTB/CompilationProfiles-example.cfg` file. This is a hastily kludged compilation profile; adjust as you need/wish.

With `kvtools`, the resulting `.bsp` file can be directly imported in Unity. This requires a "KDCBSP Workspace Config" (creatable as an asset) to map materials and set world scale.

Some particular notes:

* You need a dummy entity in each world 'cavity' so the map compiler knows the inside and outside.
* Lightmapping is still handled by Unity, so don't bother running the Q2BSP `light`.
* **Entities are not supported, but the BSP compiler may have 'built-in' entities. See <https://ericw-tools.readthedocs.io/en/latest/qbsp.html#compiler-internal-bmodels>.**
* On some versions, `func_detail_illusionary` defaults to `"_mirrorinside" "1"`. _**Make sure to explicitly change it to 0, or it'll horribly break light baking!!!**_
* If the Unity material is None, triangles will not be created. This is one of the two useful ways to use `common/sky` (the other being a skybox material, perhaps with a custom shader with emission).
* Special materials (note: The meanings are primarily assigned using `ericw-tools` metadata `.wal_json` files):
	* If looking for `skip` / `caulk`: These are Q1 and Q3 names of `common/nodraw`.
	* `common/areaportal`: Areaportals are not presently used. This is included for completeness. Ideas have been thrown around of splitting meshes by this.
	* `common/clip`: Invisible collision. Distinct from `nodraw` in that it has no effects on BSP/sealing/face-cutting etc.
	* `common/hint`: Just manipulates BSP splitting. (We usually never care, and this is included only for completeness.)
	* `common/noclip`: **Extremely special custom tool for this importer, used to integrate multiple BSPs or BSP and traditional content.** (See below.)
	* `common/nodraw`: Invisible, but BSP compiler treats it as if it was opaque. Player can't walk through it. Seals the map.
		* Use for surfaces that always face away from the player but that would traditionally still be considered 'visible'.
		* _You do not need to `nodraw` the exterior of the map._ Assuming the map isn't 'leaking', the BSP compiler will automatically delete the exterior faces.
	* `common/origin`: Controls brush-model origin. Included only for completeness.
	* `common/sky`: More-or-less regular material intended to be setup by mapper. Traditionally reserved for skybox.
	* `common/trigger`: See `nodraw`. The 'can't walk through it' property is usually resolved by being part of an appropriate brush entity, but these aren't supported. Included only for completeness.
* While most materials act as per 'usual BSP standards', the `common/noclip` material is special. It's `common/nodraw` **without collision.**
	* This means it:
		* Still seals leaks like `common/nodraw`, so you can use it at the map's edge (which is the intended purpose)
		* Still cuts faces like `common/nodraw`
		* Still doesn't display anything like `common/nodraw`
		* But the player can walk through it (_and immediately become 'out of bounds' relative to the current map!_)
	* This is the ideal glue between `.map` files or between `.map` and modelled content.
	* This separation may be necessary to split larger maps into individual lightmaps.
	* This separation is required for occlusion culling, whether trigger-based or Unity occlusion.
	* Technically, it carries `SOLID | CURRENT_0` contents flags (`CURRENT_0` is used as a marker by the importer to mean 'not actually solid'), and `NODRAW` face flags.

## Proxy Assets

_Experimental!_ (I'm not sure if the asset 'importing' technique here has any odd effects.)

Proxy assets prevent Unity from breaking references when the source file is lost.

They are text files with extension `.proxyasset`. Their content is solely a relative path name (`..` may not be supported, so don't try it).
