# `kvtools`

`kvtools` is a Unity package in this repository, at <https://github.com/20kdc/vrc-stuff/tree/main/kvtools>.

It contains:

* Datamining infrastructure used to create and maintain this documentation.
* `.udonjson` support for importing and using precompiled Udon programs
* Tools to dump compiled Udon programs in your Worlds project

## `.udonjson`

The `kvtools` set uses Unity JSON files with the extension `.udonjson` to represent `SerializedUdonProgramAsset`s for direct use in UdonBehaviour.

This is given as `.unity.json` in the dumper to avoid dumping files made by the dumper.

When writing out from within Unity, `EditorJsonUtility.ToJson` specifically _must_ be used, as otherwise file references are lost.

## `VRChat SDK/KDCVRCTools/Dataminer`

This menu option will create `Assets/datamine.txt`. The format of this is _really_ subject to change, more so than the rest of this, even; the format is solely meant to be an interface between it and `datamine2json.py`.

## `datamine2json.py`.

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

### `VRChat SDK/KDCVRCTools/Dump Odin`

This transforms `SerializedUdonPrograms` contents into `KDCVRCDumpUdonJSON`.

This contains `.odin.bin` and `.odin.json` files representing the extracted OdinSerializer contents.
