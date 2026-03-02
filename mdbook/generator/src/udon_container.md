# `SerializedUdonProgramAsset`

The 'root' of an Udon program's existence is in the `SerializedUdonProgramAsset` passed to `UdonBehaviour`.

This is a pretty standard Unity asset, with a few key fields:

* `serializedProgramCompressedBytes`: `byte[]` containing GZip'd Udon program.
* `serializedProgramBytesString`: Legacy method of storing the bytes. If not an empty string, base64-encoded Udon program.
* `serializationDataFormat`: This refers to the OdinSerializer data format -- in practice this will always be 0 (`DataFormat.Binary`).
* `networkCallingEntrypointMetadata`: Array of `NetworkCallingEntrypointMetadata`. Used for 'validated RPC'; ClientSim in particular checks this. This is described at the end of the document.
* `programUnityEngineObjects`: This is a list, used to encode references to i.e. Unity resources from within the program binary blob.

## Program Format

The Udon program itself is stored in the asset as a GZip'd [OdinSerializer](./odinserializer.md) blob.

To follow along with the structures, please use <https://github.com/20kdc/vrc-stuff/blob/main/udon/docs/docExample.odin.json>. The corresponding Udon Graph is kept at <https://github.com/20kdc/vrc-stuff/blob/main/kvassets/Assets/docExample.asset>.

The "JSON" file is written using "OdinSerializer JSON", which is not really JSON. But it makes a good, clear, easy to access reference, so that's what'll be used here.

Programs are a `VRC.Udon.Common.UdonProgram` instance, consisting of, **in this order**:

* `InstructionSetIdentifier` (`UDON`)
* `InstructionSetVersion` (1)
* `ByteCode`: A byte array of **big-endian** uint32 values. These are addressed by their indexes multiplied by 4, even though the accesses can't be offset at all.
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
