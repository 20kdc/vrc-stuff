# 'KIP32 ABI'

This describes the format/binary interface of the ELFs read by `kip32ingest`.

To summarize, you're compiling for what is essentially a RV32I microcontroller. Link with `sdk/kip32.ld` linker script.

## CPU/Memory

The virtual CPU is the unprivileged set of RV32I, somewhat akin to a `picorv32` core.

Unlike such a core, there's enforced W^X; the recompilation does not include a 'fallback interpreter'.

In addition, it is undefined behaviour for code to jump to any misaligned or non-executable location.

The handling of unaligned accesses is environment-specific, but may vary between 'corrupt', 'error' and 'fine'.

While RISC-V is Neumann-style, KIP32 is 'Harvard-style pretending to be Neumann'.

The memory is laid out as a flat array that starts at address 0. Without intervention, all code can be read, though not written.

However, the `.kip32_zero` section naming can erase code for better compression (or obfuscation). Assuming this memory consists solely of instructions not actually read as data by the program, it will still execute perfectly.

Other Harvard-esque shenanigans exist in regards to the Virtual Code Addresses section, and also in the nature of the W^X restriction.

Finally, the flat memory array is _implicitly_ (this has no visible effect) divided into the section before the first zero byte and the region including and after it.

The latter section is not included in the 'data image', and is thus safe.

## ELF format

Theoretically, `kip32ingest` loads one or more ELFs into memory.

Practically, a single ELF with a start address of 0 is the most efficient option.

The loader works based on section headers, but the ELF must be completely linked -- relocations do not function.

The loader somewhat respects these section flags:

* `SHF_ALLOC`: If this flag is not present, the section is ignored.
* `SHF_NOBITS`: The section's data is read as all-zeros.
* `SHF_EXECINSTR`: The maximum (highest) end address of these instructions is used to divide code from data to limit the amount of erroneously compiled instructions.
	* This implies it is most efficient for these sections to all be as close to 0 as possible and to solely consist of decodable instructions. Due to the nametable syscall ABI, this isn't perfectly true, but it mostly holds.

Special sections are determined based on prefixes:

* `.kip32_export`: Symbols here are entrypoints.
* `.kip32_zero`: _When generating the data image,_ all data in this section is zeroed out.
	* You can think of this as _execute-only._
	* A general rule here is that you should only use it if you trust your compiler to behave itself.

There are also these special symbols:

* `_stack_start`: If provided, the initial value of SP. If **not** provided, then stack space is automatically allocated.
	* Rationale: Automatic allocation of stack space allows easy adjustment of stack size without changing the link script.

## Virtual Code Addresses

A number of 'virtual code addresses' may exist.

These are addresses for which execution would _**usually**_ result in undefined behaviour, but which instead result in behaviour which is backend-defined.

These may be placed at a number of locations, or multiple simultaneously:

* In negative addresses
* Immediately after the end of instructions, overlapping data

## Invocation

No code is _inherently_ executed on startup. If initializers are required, this will need to be negotiated with the embedder (for example, Udon might use `_start`).

However, on VM reset:

* `sp`/`x2` is set to the end of stack memory (if allocated) or `_stack_start` (if specified).
	* This only happens here; if the VM leaves SP in the wrong place, it _stays there._ This was done to allow for reentrancy.
* Memory is reset to the initial data image.

Notably:

* Other than `sp`, no registers are _inherently_ reset on VM reset. If reset needs to be detected, a flag may be kept in memory.

Entrypoints are taken from `.kip32_export` sections.

When an entrypoint is invoked, the sequence goes something like this:

1. `a0`-`a7` may be overwritten by the caller via some method.
	* The caller _can_ write `sp`, but it should only do this _if it's really sure of what it's doing, especially as the change to SP will be lost if the VM isn't initialized._
2. VM reset is performed if necessary.
	* This process doesn't interfere with `a0`-`a7`, but it does reset memory. Luckily, the caller will usually need to retrieve a symbol first, and this can act as a 'probe' to ensure VM init.
3. `ra` is overwritten with an 'abort' virtual code address.
4. Once the VM aborts, the caller may read back `a0`-`a7` via some method.

## Syscalls

There are two syscall implementations, 'legacy'/`ECALL` and nametable/`EBREAK`.

`ECALL` syscalls consist solely of that instruction, and use an implementation-specific mechanism to execute user-provided code.

`EBREAK` syscalls rely on the instruction fusion apparatus\*, and consist of that instruction followed by a 32-bit word giving the address of a C string.

_This address and C string are considered part of the instruction, and are safe to erase in the data image._ The instruction will be decoded as a regular EBREAK if the address does not result in a valid UTF-8 C string.

Memory access during syscalls or between invocations is both possible and expected.

If the syscall may cause reentrancy, the following registers **must** be saved:

* `x1`/`ra`
* `x5`/`t0`
* `x6`/`t1`
* `x7`/`t2`
* `x28`/`t3`
* `x29`/`t4`
* `x30`/`t5`
* `x31`/`t6`
* While `a0`-`a7` are implicitly syscall arguments/return values, and thus should be _implicitly_ 'saved', if only some of them are used, they should still all be saved.

\* In principle, KIP32 could save data image bytes by using the compressed extension. The problem is that the amount of 'dummy' recompiled code would massively increase. \
For this reason, the instruction parsing apparatus was built around the idea that one word is one 'source instruction'. \
Nametable syscalls cause a few wasted instruction 'slots,' but allow inlining syscalls, which ultimately improves reentrancy for the target. \
The architecture required for instruction fusion was added mainly as a side-effect.

## Udon Supplement

The argument registers `a0`-`a7` are directly exposed as public `SystemInt32` fields.

It should be reasonably possible using an include to setup network call metadata to invoke RISC-V code remotely without an intermediary.

The expected workflow for other behaviours is to set the `a0`-`a7` registers, execute a custom event, and retrieve results.

Custom events called `_sym_` are created for any reasonably valid symbol. These events don't actually start the VM, but they return the address of the symbol in `a0`, for easy DMA.

The `vm_memory` field contains DMA code. It isn't synchronized, so you need to implement sync manually one way or another.

Undefined jumps to negative or extremely high addresses should be 'reasonably safe' causing an immediate VM abort. Undefined jumps to early non-executable addresses i.e. the early data image will result in chaos.

Regarding syscall implementation, the `--inc` flag allows adding `kudonasm` assembly (KU2) to the result.

If possible, KU2 is transpilable into Udon Assembly. Certain KU2 features don't result in 'clean' Udon Assembly, and certain KU2 features _can't_ be transpiled. Work with caution.

**The transpiled code keeps the Udon stack clean -- therefore, values may safely be left on the Udon stack between syscalls.**

Nametable syscalls are _mostly_ inlined KU2 defined in `--inc` files or `stdsyscall.ron`. The exceptions are the following special builtin syscalls:

* `builtin_extern_*`: Calls the extern `*` (substitute with whichever Udon extern you want).
	* This is compiled into the KU2 instruction `extern(EXT("*"))`, or UASM instruction `EXTERN, "*"` (kind of)
* `builtin_push_*`: The contents of `*` are parsed as a KU2 operand.
	* This is compiled into the KU2 instruction `push(*)`. UASM varies, but a `PUSH, ` is always involved.

Legacy `ecall` syscalls meanwhile call a single assembly routine. They set the indirect jump pointer for return to be possible, so either this must be properly backed up, or else reentrancy is not possible.

Note that it is reasonably trivial to write non-inlined nametable syscalls if reentrancy is not required for those syscalls, so the only reason to use the `ecall` syscall mechanism is with a precise and specific goal in mind.

Tighter integration may be achieved using various flags, particularly `--inc`; see transpiler help for details.

Also see `sdk/stdsyscall.ron`.
