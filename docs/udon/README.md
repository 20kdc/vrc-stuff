# UdonVM: A Primer (Live Copy)

Udon is VRChat's sandboxed virtual machine.

Udon operates by moving around values of the C# `object` type, with heavy type-checking (using `StrongBox` as evidenced from serialized Udon data), and running "externs" that manipulate these values.

It also has a conditional jump, an indirect jump, and an integer stack _solely_ consisting of hardcoded integers.

It is extremely minimalist at its core, with only 9 opcodes.

## `SerializedUdonProgramAsset`

The 'root' of an Udon program's existence is in the `SerializedUdonProgramAsset` passed to `UdonBehaviour`.

This is a pretty standard Unity asset, with a few key fields:

* `serializedProgramCompressedBytes`: `byte[]` containing GZip'd Udon program.
* `serializedProgramBytesString`: Legacy method of storing the bytes. If not an empty string, base64-encoded Udon program.
* `serializationDataFormat`: This refers to the OdinSerializer data format -- in practice this will always be 0 (`DataFormat.Binary`).
* `networkCallingEntrypointMetadata`: Array of `NetworkCallingEntrypointMetadata`. Used for 'validated RPC'; ClientSim in particular checks this. This is described at the end of the document.
* `programUnityEngineObjects`: This is a list, used to encode references to i.e. Unity resources from within the program binary blob.

_**A key scary note:**_

Because of a whole load of complications (see `EDITOR.md`), the `kvtools` set uses Unity JSON files with the extension `.udonjson` to represent `SerializedUdonProgramAsset`s.

This is given as `.unity.json` in the example files and in the dataminer (mainly so that we don't get a recursively importy mess).

## Odin Serializer

The Udon program's bytes (after base64, or gzip, or etc.) are encoded using a vendored OdinSerializer.

Somewhat conveniently, `Packages/com.vrchat.worlds/Runtime/Udon/Serialization/OdinSerializer/Version.txt` tells us _which_ version.

As of this writing, commit [`8d9fc0bca118d9c6f927ee2fb23330138a99cbf2`](https://github.com/TeamSirenix/odin-serializer/tree/8d9fc0bca118d9c6f927ee2fb23330138a99cbf2) is used. _For this section, file references will be made relative to that repository._

To find differences, it's easiest to clone <https://github.com/TeamSirenix/odin-serializer>, checkout the appropriate commit, delete the OdinSerializer directory in the checkout, copy the OdinSerializer directory from the VRChat Worlds package to the checkout, then remove all `.meta` files up to 4 directories deep in the checkout.

The main points of note are:

1. Any 'risky' Formatters (used to serialize/deserialize various types of object) have been disabled via various methods. A particular focus has been on removing weak references.
2. There are some API changes owing to the removal of weak references.
3. There are various performance tweaks.

Importantly, _**there is no difference in the serialization format.**_

Therefore, what follows is a summary of the OdinSerializer core format, based on the upstream code.

### Concepts

Conceptually, OdinSerializer data is stored as a stream of _entries,_ with a final 'end of stream' entry delimiting serialization.

There are three different kinds of entries:

* Entries that encode values or the beginning of compound values. (I call these 'Values'.)
	* These entries are evidenced by having Named and Unnamed variants.
	* The compound values encodable this way are References (objects) and Structs (value types).
		* The `StartOfReferenceNode` values have Node IDs, which are are particularly important; these match up to the `InternalReference` values.
		* On the C# side, the contents of these compounds are encoded using Formatters; this is the Serializer/Formatter barrier.
* Entries solely used inside other compounds that don't properly decode to values alone.
	* `PrimitiveArray` and `StartOfArray` are good examples of this. They don't have the named/unnamed tagging, and they're reference types anyway, so they need to live in a reference node.
* Entries that end compound nodes (`EndOfNode` and `EndOfArray`).

The data reader/writer implementation has a lot of flexibility in how it _arrives_ at these entries, and there is a notion of a tree structure built within the 'flat' entry list using the `StartOf` and `EndOf` entries.

Still, it's possible to read/write entries without knowing the higher-level types they're used to construct, and the entry types are arranged such that it's similarly possible to read/write the entry _tree_ without that knowledge.

**However, a 'sensible' interpretation of things like i.e. array length will lead to failure.** See SerializationFormatter notes below.

The reading of an entry is divided into the _header_ (reads name, type) and the _content_ (anything specific to that entry type).

When reading an entry, `PeekEntry` is used to read the header, and then the appropriate read function is used to read the content.

`SkipEntry` in [`BaseDataReader`](https://github.com/TeamSirenix/odin-serializer/blob/8d9fc0bca118d9c6f927ee2fb23330138a99cbf2/OdinSerializer/Core/DataReaderWriters/BaseDataReader.cs#L397), meanwhile, skips over the overall tree structure.

To summarize how this fits together, observe the following tree:

* Serializer: Operates at entry level. Specialized formatters are used for anything encoded _in a single entry._ Has access to field information.
	* All base integer types are serializers.
	* ComplexTypeSerializer: Encodes null directly, or wraps in start-of-reference-node or start-of-struct-node as appropriate.
		* Assuming there is something to encode, finds the appropriate formatter and passes control to the formatter layer.
		* **ComplexTypeSerializer is used for all reference types except String.**
* Formatter: Operates _within_ a reference or struct node. Implements encoding/decoding the contents.

A serializer is chosen using the _field's type_ (and has to worry about propagating the field name) while a formatter is chosen using the _object's type_ (and doesn't).

Reference/struct serializers wrap the formatters with the appropriate start/end entries. (Note, however, it can theoretically be a complete free-for-all on named/unnamed fields _inside_ the node.)

Another problem to keep in mind is that there's no distinction between a named and unnamed array. This is because arrays are reference types -- **all reference types except `String` are wrapped appropriately.** (OdinSerializer treats String as a value type for encoding purposes.)

The following example trace shows how an array field is encoded:

```
Value(Some("ExportedSymbols"), StartRefNode(TypeID(13), 22)),
StartOfArray(2),
Value(None, Primitive(String("message"))),
Value(None, Primitive(String("syncMe"))),
EndOfArray,
EndOfNode,
```

#### `SerializableFormatter`

For some reason, it's common to see types use the following arrangement:

```
Value(None, StartRefNode(TypeName(18, "VRC.Udon.Common.UdonSyncProperty, VRC.Udon.Common"), 27)),
StartOfArray(2),
Value(Some("type"), Primitive(String("System.String, mscorlib"))),
Value(Some("Name"), Primitive(String("this"))),
Value(Some("type"), Primitive(String("VRC.Udon.Common.Interfaces.UdonSyncInterpolationMethod, VRC.Udon.Common"))),
Value(Some("InterpolationAlgorithm"), Primitive(ULong(1))),
EndOfArray,
EndOfNode,
```

This is the fault of `SerializableFormatter`, which, as a format goes, looks like a chip in an otherwise very pretty glass vase.

### Binary Format

The OdinSerializer binary format is little-endian.

For the binary format (the one we're interested in), the format follows this exact pattern; the file can be precisely reduced to a reasonably flat list of entries.

The type of each entry is defined by a single byte at the start of that entry. Udon programs, for instance, start with `0x02: UnnamedStartOfReferenceNode`.

The binary entry types are listed at [`BinaryEntryType`](https://github.com/TeamSirenix/odin-serializer/blob/8d9fc0bca118d9c6f927ee2fb23330138a99cbf2/OdinSerializer/Core/DataReaderWriters/Binary/BinaryEntryType.cs).

Note the Named/Unnamed distinction. Also note `TypeName`/`TypeID` -- these are not _really_ entry types at all, but live in an overlapping namespace of 'entry types' valid when reading a Type.

With all of this in mind, the functions to pay attention to in [`BinaryDataReader`](https://github.com/TeamSirenix/odin-serializer/blob/8d9fc0bca118d9c6f927ee2fb23330138a99cbf2/OdinSerializer/Core/DataReaderWriters/Binary/BinaryDataReader.cs) are:

* L93: `PeekEntry`: Reads the header of any type of entry.
	* The header only contains the entry type byte, and (for any `Named*` entry type) a string value for the field name.
* L1823: `ReadStringValue`: Defines the format of a string.
	* Reads a byte, and then an int32. If the byte is 0, the string is written in 8-bit Latin-1 (`U+0000` through `U+00FF`); if the byte is 1, it is written in little-endian UTF-16. The int32 represents the length of the string in characters.
* L1953: `SkipPeekedEntryContent`: Reads the content of any type of entry.
	* Reference nodes contain a type entry followed by an int32 node ID. Negative node IDs don't count.
	* Struct nodes contain a type entry.
	* Array starts contain a length int64.
	* Primitive arrays contain a length int32 and a bytes-per-element int32. These are multiplied to the final length.
	* Primitive types in general are their obvious sizes.
	* Internal references and external index references are int32 node IDs.
	* GUIDs, external references using GUIDs, and 'Decimal' values, are 8 bytes.
	* Strings and external references using strings contain string values.
	* Nulls and end-of entries have no contents.
* L2092: `ReadTypeEntry`: Defines the type format.
	* The type format starts with an 'entry type' byte. In practice that the byte here shares namespace with entry types seems to be more to avoid defining another enumeration.
	* If the byte is `UnnamedNull`, null is returned.
	* If the byte is `TypeID`, the type is read using an int32 type ID; no further data is given.
	* If the byte is `TypeName`, an int32 type ID is read (to cache the type in), and a string value is read (the name of the type).
	* Otherwise, it's invalid.

## Program Format

To follow along with the structures, please use `docExample.odin.json`. The corresponding Udon Graph is kept in `kvassets`.

The "JSON" file is written using "OdinSerializer JSON", which is not really JSON. But it makes a good, _reliable_ (unlike Udon Assembly), easy to access reference, so that's what'll be used here.

Programs are a `VRC.Udon.Common.UdonProgram` instance, consisting of, **in this order**:

* `InstructionSetIdentifier` (`UDON`)
* `InstructionSetVersion` (1)
* `ByteCode`: A byte array of little-endian uint32 values. These are addressed by their indexes multiplied by 4, even though the accesses can't be offset at all.
* `Heap`: Also known as `VRC.Udon.Common.UdonHeap`. Constant and dynamic values through the life of the program. This is an array of values; a "heap index" is an index into this array.
* `EntryPoints`: This is a `VRC.Udon.Common.UdonSymbolTable` mapping names to code addresses, with a second list of "exported" symbols. (Event handlers are exported.)
* `SymbolTable`: This is a `VRC.Udon.Common.UdonSymbolTable` mapping names to heap addresses, with a second list of "exported" symbols. (Exported symbols are visible in the Unity editor.)
* `SyncMetadataTable`: A `VRC.Udon.Common.UdonSyncMetadataTable`. Controls which variables will be synced and how.
* `UpdateOrder`: Indicates the order in which Update will be run per-frame.

Some key notes:

* There is a custom `UdonProgramFormatter` at play. It is not particularly flexible - _fields must be in their exact order. Names may as well be ignored._
* The code must purely consist of _a_ valid instruction sequence, though misinterpretation of parameters as code is allowed and may bypass the intent of this rule.
* Non-exported symbols are still important to execution, such as `onStringLoadSuccessIVRCStringDownload`: This variable is not exported, but the `_onStringLoadSuccess` event handler presumably uses it to store the parameter.

If non-exported entrypoints are important is unknown.

### `VRC.Udon.Common.UdonHeap`

* `HeapCapacity`: The capacity of the heap. Usually `512`.
* `HeapDump`: So this is a `List<(uint32, IStrongBox, System.Type)>` making up the actual contents of the heap. The `StrongBox` instance contains the actual value. This feels overcomplicated.
	* The first value is the heap index. The second value is the contents. The third value is the type.
	* A particular type, `VRC.Udon.Common.UdonGameObjectComponentHeapReference`, contains only a `System.Type`, which is whatever is being retrieved. This is the outcome of `this` in Udon Assembly.
		* This is resolved by `UdonBehaviour`; the valid types here are `GameObject`, `Transform`, `UdonBehaviour`, `IUdonBehaviour`, and `Object`.
		* `GameObject` and `Transform` are reasonably obvious, while the last three all resolve to the `UdonBehaviour`.

### `VRC.Udon.Common.UdonSymbolTable`

* `Symbols`: A `List<VRC.Udon.Common.Interfaces.IUdonSymbol>`, containing `VRC.Udon.Common.Interfaces.UdonSymbol` instances:
	* `Name`: `string`: The name of the symbol.
	* `Type`: `System.Type`: The type of the symbol. `null` for entrypoints.
	* `Address`: `uint32` of the symbol's address (code address or heap index).
* `ExportedSymbols`: A `List<string>` of exported symbols.

### `VRC.Udon.Common.UdonSyncMetadataTable`

* `SyncMetadata`: `List<VRC.Udon.Common.Interfaces.IUdonSyncMetadata>`, containing `VRC.Udon.Common.UdonSyncMetadata` instances:
	* `Name`: `string`: The name of the symbol.
	* `Properties`: `List<VRC.Udon.Common.Interfaces.IUdonSyncProperty>`, containing `VRC.Udon.Common.UdonSyncProperty` instances:
		* `Name`: `string`: The name of the property. Seems always set to `"this"`.
		* `InterpolationAlgorithm`: `VRC.Udon.Common.Interfaces.UdonSyncInterpolationMethod`: The interpolation method.
			* 0: `none`
			* 1: `linear`
			* 2: `smooth`

### Runtime

At runtime, the program is instantiated inside an `UdonBehaviour` (see `Packages/com.vrchat.worlds/Runtime/Udon/UdonBehaviour.cs`). The instantiated instance lasts for the lifetime of the component.

In theory, everything above is treated as mutable. In practice, the `Heap` field is the only mutable field I'm aware of, but that might not match reality.

Two additional elements exist at runtime:

* `VRC.Udon.Common.UdonGameObjectComponentHeapReference` objects that have symbols pointing to them are resolved before execution starts.
* The current position in the program, represented in `ByteCode` bytes.
* A stack: This is a stack of uint32 values setup at runtime. Notably, it may be possible for this stack to overflow if the program exits between events without properly clearing the stack.

## Entrypoints

Entrypoints, practically, represent event handlers. They _can_ also be used for debug symbols, though the mechanism of this is unclear.

The first two entrypoints that run are `_onEnable` and `_start`, in that order. There is no gap between them in this initial run. These will always run before any other entrypoint; if something attempts to bypass that, the invocation will be ignored. Note that `_onEnable` can be run more than once.

Here are some entrypoints with their parameter symbols in Udon Assembly, as an example.

All custom events are literal (no underscore prefix) and take no parameters.

```
.export _onTriggerEnter2D
onTriggerEnter2DOther: %UnityEngineCollider2D, null
.export _onStringLoadSuccess
onStringLoadSuccessIVRCStringDownload: %VRCSDK3StringLoadingIVRCStringDownload, null
```

Should an entrypoint ever need to return a value to VRChat, the symbol `__returnValue` has been reserved for this purpose.

## Heap And Data Symbols

The heap is an array. You can pick the size using `HeapCapacity` up to a limit of `0x100000`, which would be 8MB of raw pointers, but in practice could be more because of strongboxes and interface nonsense.

VM-wise, the acceptable values are entirely dependent on what the serialization expects. This is distinct from the assembly, which is very limited. The current visible extent of this disparity is if you attempt to use `System.Type` objects, but anything registered with the vendored OdinSerializer is likely acceptable, and possibly more besides.

It is worth noting that the C# types given here _change at runtime._ They are mainly for the benefit of the getter functions (which sometimes type-check), error messages (which show the types), and the assembler (which for some reason cares).

## Sync Metadata

Sync metadata is "two-levelled". Details not known. For now we'll just pretend that sync metadata is completely normal and corresponds directly to symbols in all cases; I have only ever seen the property name `"this"`.

Having sync metadata at all makes the value synced. There is also an interpolation method, which can be `none`, `linear` or `smooth` assembly-wise.

Unfortunately, code is not available for the interpolator.

The full list of types that can be synced, with the methods they can be synced with, is in `Packages/com.vrchat.worlds/Runtime/Udon/UdonNetworkTypes.cs`.

At present, this covers exactly these types (referred to by C# names for simplicity):

* All the sized integer and non-decimal float types (specifically `sbyte`, `byte`, `short`, `ushort`, `int`, `uint`, `long`, `ulong`, `float`, `double`) can be synced.
	* These can be synced with `none`, `linear` or `smooth` interpolation.
* `bool`, `char`, and `string` can be synced.
	* These cannot be interpolated, so only the `none` interpolation type is available.
* Arrays of any of the above types can be synced.
	* These cannot be interpolated.
* `Vector2`, `Vector3`, and `Quaternion` can be synced.
	* These can be synced with `none`, `linear` or `smooth` interpolation.
* `Color` and `Color32` can be synced.
	* These can be synced with `none` or `linear` interpolation, but not `smooth`.
* Finally, the types `Vector4`, `Vector2[]`, `Vector3[]`, `Vector4[]`, `Quaternion[]`, `Color[]`, `Color32[]`, `VRCUrl`, `VRCUrl[]` can be synced.
	* These cannot be interpolated.

## Execution Flow

The Udon interpreter loops until an exception happens or it runs out of instructions (because of a jump beyond program bounds).

`JUMP, 0xFFFFFFFC` is the usual idiom to deliberately stop the interpreter (because of the end of the program). Any label placed at the end of the program will theoretically do. If this difference has any important side-effects is uncertain.

Related to this: It is possible for an `UdonBehaviour` to cause a sequence of events which causes an event to be invoked on itself while it is executing.

If the compiler separates all temporary variables for all entrypoints, and the entrypoint being called is not already 'in use,' this sort of recursive execution is merely a little concerning.

If the compiler does _not_ separate all temporary variables, this will become a nightmare to run away from very, very fast.

## Opcodes

### 0: NOP

This opcode does nothing. There is generally no reason to use this.

### 1: PUSH parameter

This opcode pushes an integer to the stack. Udon Assembly may give the impression that a value is being pushed; this is not the case. In these cases, it is the heap address that is being pushed.

Unless you are very dedicated to size-optimizing your Udon programs (even at the expense of runtime speed in some cases), or trying to obfuscate, there is never any reason to use this in a conditional fashion. Simply push everything immediately before `EXTERN`, `COPY` or `JUMP_IF_FALSE`.

### 2: POP

Pops an integer from the stack and discards it.

This instruction only really makes sense if you're doing something very weird with the VM.

### 3: (unassigned)

This opcode is unassigned and would fail to be decoded. If executed (say, by placing it in a parameter and jumping to it), it would cause the UdonBehaviour to disable itself, and an error to be logged stating the VM has been halted, _but the VM will not in fact be halted._

### 4: JUMP_IF_FALSE parameter

Pops a heap index from the stack and reads a `System.Boolean` from it.

If this value is false, jumps to the parameter as a bytecode position. Otherwise, continues to the next instruction.

### 5: JUMP parameter

Jumps to the bytecode position given by the parameter.

### 6: EXTERN parameter

Contrary to how this might look in assembly, the heap index given as a parameter is both read and written.

Firstly, it is read.

If it is a string, then this is the extern name, and needs to be resolved into a delegate, which is _then written to the heap index._ If it's already a delegate, this is fine.

The delegate has some metadata about how many arguments it has, so that many heap indexes are popped. These are given to the extern are given in push order (first pushed = first argument). Note that these are still heap indexes, not values.

The delegate can read or write to any heap index given. In the case of out/ref (`Ref` suffix on the type in both cases), it does write, and it doesn't bother reading for out.

The actual _implementation_ of externs is that they are autogenerated wrappers around existing C# functions encased in a shell which doesn't allow listing from external code (_at least, in theory_).

**To get a list of extern names, please see 'Using `kvtools`'. The standard extern name _format_ was documented in 'The Udon VM and Udon Assembly'.**

Two important wrinkles appear, in regards to the `this` argument and the return value (including getters). `this` is an additional parameter at the start, and the return value is an additional parameter at the end, the heap index of which is then written to.

### 7: ANNOTATION parameter

This opcode allows for a value to be inserted into the opcode stream with no side-effects whatsoever.

This may be useful for inline debug information or obfuscation; in particular, there is no rule that you can't perform "misaligned" execution.

### 8: JUMP_INDIRECT parameter

Gets a heap index from the parameter and reads a `System.UInt32` from it.

Interprets this as a bytecode position and jumps to it.

### 9: COPY

Pops two heap indexes. The value from the second heap index popped (aka the first heap index given) is copied to the first heap index popped (aka the second heap index given).

## Network Calling Metadata

Later on, VRChat added 'network call metadata'. This metadata allows for secure, and more usefully, _parameterized_ Udon RPCs.

An example of `networkCallingEntrypointMetadata` in YAML:

```
  networkCallingEntrypointMetadata:
  - _maxEventsPerSecond: 5
    _name: marbleInteract
    _parameters:
    - _name: marbleInteract_Parameter1
      _type: 11
  - _maxEventsPerSecond: 5
    _name: loadBoardByChars
    _parameters:
    - _name: loadBoardByChars_Parameter1
      _type: 21
```

Parameter names are (unexported, by apparent convention) heap symbols.

Importantly, there's the implication here that network call metadata is sort of built _around_ Udon; it's not _part of_ Udon per-se.

The type numbers are handled via `VRCUdonSyncTypeConverter`; a table is given here:

| Type Index | Udon Type |
| ---------- | --------- |
| 1 | `SystemInt16` |
| 2 | `SystemUInt16` |
| 3 | `SystemChar` |
| 4 | `SystemSByte` |
| 5 | `SystemByte` |
| 6 | `SystemInt64` |
| 7 | `SystemUInt64` |
| 8 | `SystemDouble` |
| 9 | `SystemBoolean` |
| 10 | `SystemSingle` |
| 11 | `SystemInt32` |
| 12 | `SystemUInt32` |
| 13 | `UnityEngineVector2` |
| 14 | `UnityEngineVector3` |
| 15 | `UnityEngineVector4` |
| 16 | `UnityEngineQuaternion` |
| 17 | `UnityEngineColor` |
| 18 | `UnityEngineColor32` |
| 19 | `SystemInt16Array` |
| 20 | `SystemUInt16Array` |
| 21 | `SystemCharArray` |
| 22 | `SystemSByteArray` |
| 23 | `SystemByteArray` |
| 24 | `SystemInt64Array` |
| 25 | `SystemUInt64Array` |
| 26 | `SystemDoubleArray` |
| 27 | `SystemBooleanArray` |
| 28 | `SystemSingleArray` |
| 29 | `SystemInt32Array` |
| 30 | `SystemUInt32Array` |
| 31 | `UnityEngineVector2Array` |
| 32 | `UnityEngineVector3Array` |
| 33 | `UnityEngineVector4Array` |
| 34 | `UnityEngineQuaternionArray` |
| 35 | `UnityEngineColorArray` |
| 36 | `UnityEngineColor32Array` |
| 37 | `SystemString` |
| 100 | `VRCSDKBaseVRCUrl` |
| 101 | `VRCSDKBaseVRCUrlArray` |
| 102 | `SystemStringArray` |

(To update this table, see Using The Dataminer).

## Using `kvtools`

`kvtools` is a package in this repository. It contains datamining infrastructure used to create and maintain this document.

### `VRChat SDK/KDCVRCTools/Dataminer`

This menu option will create `Assets/datamine.txt` -- copy or symlink for `datamine2json.py`, then run it.

It will create two files:
* `api.json`: JSON dump of the known Udon API of VRChat. This includes type hierarchies.
* `synctypes.md`: GFM-table-formatted table for import into the Network Calling Metadata section of this document.

### `VRChat SDK/KDCVRCTools/Dump Odin`

This transforms `SerializedUdonPrograms` contents into `KDCVRCDumpUdonJSON`.

This contains `.odin.bin` and `.odin.json` files representing the extracted OdinSerializer contents.
