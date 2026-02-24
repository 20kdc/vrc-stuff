# Legacy Notes

The notes are part of the original research from my internal repository that eventually became my `Add a page on the Udon VM and Udon Assembly. #136` creator-docs PR.

By extension, they predate the network event ratelimit/security extensions.

I expect them to become relevant if/when I take the logical next step in the project, _direct Udon compilation._

Some similarities in wording are to be expected; what you see here is the internal/research side.

Research was performed partially by getting OdinSerializer to dump using the OdinSerializer JSON dump format (this format is _not_ reliably reversible, but it's decent for viewing).

Very important errata:

* **ByteCode** is described here as little-endian. It's big-endian.

 \- 20kdc

# UdonVM: A Primer

Udon is VRChat's sandboxed virtual machine.

Udon operates by moving around values of the C# `object` type, with heavy type-checking (using `StrongBox` as evidenced from serialized Udon data), and running "externs" that manipulate these values.

It also has a conditional jump, an indirect jump, and an integer stack _solely_ consisting of hardcoded integers.

It is extremely minimalist at its core, with only 9 opcodes.

## Program Format

To follow along with the structures, please use `helloWorld.odin.json`. The corresponding Udon Assembly is `helloWorld.uasm`.

The "JSON" file is written using "OdinSerializer JSON", which is not really JSON. But it makes a good, _reliable_ (unlike Udon Assembly), easy to access reference, so that's what'll be used here.

Programs are a `VRC.Udon.Common.UdonProgram` instance, consisting of:

* `InstructionSetIdentifier` (`UDON`) and `InstructionSetVersion` (1).
* `ByteCode`: A byte array of little-endian uint32 values. These are addressed by their indexes multiplied by 4, even though the accesses can't be offset at all.
* `Heap`: Also known as `VRC.Udon.Common.UdonHeap`. Constant and dynamic values through the life of the program. This is an array of values; a "heap index" is an index into this array.
* `EntryPoints`: This is a `VRC.Udon.Common.UdonSymbolTable` mapping names to code addresses, with a second list of "exported" symbols. (Event handlers are exported.)
* `SymbolTable`: This is a `VRC.Udon.Common.UdonSymbolTable` mapping names to heap addresses, with a second list of "exported" symbols. (Exported symbols are visible in the Unity editor.)
* `SyncMetadataTable`: A `VRC.Udon.Common.UdonSyncMetadataTable`. Controls which variables will be synced and how.
* `UpdateOrder`: Indicates the order in which Update will be run per-frame.

Some key notes:

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

Two important wrinkles appear, in regards to the `this` argument and the return value (including getters). `this` is an additional parameter at the start, and the return value is an additional parameter at the end, the heap index of which is then written to.

### 7: ANNOTATION parameter

This opcode allows for a value to be inserted into the opcode stream with no side-effects whatsoever.

This may be useful for inline debug information or obfuscation; in particular, there is no rule that you can't perform "misaligned" execution.

### 8: JUMP_INDIRECT parameter

Gets a heap index from the parameter and reads a `System.UInt32` from it.

Interprets this as a bytecode position and jumps to it.

### 9: COPY

Pops two heap indexes. The value from the second heap index popped (aka the first heap index given) is copied to the first heap index popped (aka the second heap index given).
