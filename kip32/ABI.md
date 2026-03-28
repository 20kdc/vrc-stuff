# 'KIP32 ABI'

This describes the format/binary interface of the ELFs read by `kip32ingest`.

To summarize, you're compiling for what is essentially a RV32IM microcontroller. Link with `sdk/kip32.ld` linker script.

## CPU/Memory

The model CPU is the unprivileged set of RV32IM, somewhat akin to a `picorv32` core with multiply/divide unit.

_Note that non-Udon targets in development or in truly 'turing tarpit' environments might not have the M extension, or may only support specific instructions._

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
	* This implies it is most efficient for these sections to all be as close to 0 as possible and to solely consist of decodable instructions. This isn't perfectly true if using `EBREAK` syscalls, but it mostly holds.

Special sections are announced by the prefix `.kip32_` followed by a set of `_`-separated tags:

* `.kip32_export`: Symbols here are entrypoints. Note that this implies these symbols are _always_ assumed to be code.
	* Other symbols that have bindings indicating export are potentially _available,_ but not _entrypoints._
* `.kip32_zero`: _When generating the data image,_ all data in this section is zeroed out.
	* You can think of this as _execute-only._
	* A general rule here is that you should only use it if you trust your compiler to behave itself.
* `.kip32_metadata`: Metadata. This isn't included in the image, even if `SHF_ALLOC` is set. This has a number of consequences:
	* It is _mostly_ okay for these sections to contain duplicate or obscenely large entries if required.
		* Some backend-specific strings may have effects which, if duplicated, cause issues.
	* As it's not _really_ part of the loaded memory image, it doesn't (inherently) increase the amount of memory used.
		* It may have an arbitrary start address, but it should not be located inside the code section as this would cause nametable syscalls to interfere with regular code.
		* Typically, this should be the second-to-last last section you link.
	* All sections must contain a series of null-terminated UTF-8 strings. Where not empty, these strings are prefixed with an identifier followed by a colon to indicate purpose.
		* The 'universally defined' one is: `syscall:`; the addresses of these strings are special.
		* Null padding is simply read as a series of empty strings, which are ignored.
	* The **last metadata section** (which should typically be the only one) is considered '_the_' metadata section; metadata _strings_ are well-defined, but they cannot be indexed by address.
		* The distinction between 'last metadata section' is to catch a hypothetical Sufficiently Clever Compiler which might attempt to merge suffixes.
* `.kip32_discard`: Similar to `.kip32_metadata`, but without the semantics.
	* This may also be achieved using a `NOLOAD` output section type in the linker script.

There are also these special symbols:

* `_stack_start`: If provided, the initial value of SP. If **not** provided, then stack space is automatically allocated.
	* Rationale: Automatic allocation of stack space allows easy adjustment of stack size without changing the link script.

## Virtual Code Addresses

A number of 'virtual code addresses' may exist.

These are addresses for which execution would _**usually**_ result in undefined behaviour, but which instead result in behaviour which is backend-defined.

These may be placed at a number of locations, or multiple simultaneously:

* In negative addresses
* Immediately after the end of instructions, overlapping data

`syscall:` metadata is _almost_ a strictly well-defined Virtual Code Address, but for complexity reasons may only be accessed using typical `jal ra,` instructions (aka `call`).

## Invocation

No code is _inherently_ executed on startup. If initializers are required, this will need to be negotiated with the embedder (for example, Udon might use `_start`).

However, on VM reset:

* `sp`/`x2` is set to the end of stack memory (if allocated) or `_stack_start` (if specified).
	* This only happens here; if the VM leaves SP in the wrong place, it _stays there._ This was done to allow for reentrancy.
* `gp`/`x3` is set to `__global_pointer$` (if the symbol exists) or `0` (if not).
	* <https://maskray.me/blog/2021-03-14-the-dark-side-of-riscv-linker-relaxation> is an interesting article; basically, GCC just _decided_ to declare the EABI spec canonical across the Unix ABI as well. In order to prevent issues, it's better to just try and support this.
* Memory is reset to the initial data image.

Notably:

* Other than `sp` and `gp`, no registers are _inherently_ reset on VM reset. If reset needs to be detected, a flag may be kept in memory.

Entrypoints are taken from `.kip32_export` sections.

When an entrypoint is invoked, the sequence goes something like this:

1. `a0`-`a7` may be overwritten by the caller via some method.
	* The caller _can_ write `sp`, but it should only do this _if it's really sure of what it's doing, especially as the change to SP will be lost if the VM isn't initialized._
2. VM reset is performed if necessary.
	* This process doesn't interfere with `a0`-`a7`, but it does reset memory. Luckily, the caller will usually need to retrieve a symbol first, and this can act as a 'probe' to ensure VM init.
3. `ra` is overwritten with an 'abort' virtual code address.
4. Once the VM aborts, the caller may read back `a0`-`a7` via some method.

## Syscalls

There are three ways of expressing a syscall.

1. 'legacy' `ECALL` syscalls consist solely of that instruction, and use an implementation-specific mechanism to execute user-provided code.
2. `EBREAK` syscalls rely on the instruction fusion apparatus\*, and consist of that instruction followed by a 32-bit word giving the virtual address of a C string in the (as in, final) `.kip32_metadata` section.
	* _This address is considered part of the instruction, and is safe to erase in the data image._
	* The instruction will be decoded as a regular EBREAK if the address does not result in a valid UTF-8 C string in the `.kip32_metadata` section.
3. Call-Into-Data syscalls are similar to EBREAK, but are triggered using `JAL ra, string_symbol` (aka `CALL string_symbol`).
	* This has the benefit of not potentially confusing the instruction decoder.
	* _**Critically, `ra` is not actually altered.**_
		* This is 'valid behaviour' insofaras `ra` is a caller-saved register and thus the callee may leave it at any arbitrary value.
		* Assembly-code thunks depend on this.

Which of these methods to use depends on your goals.

* `ECALL` is lower performance as dynamic dispatch is a necessity, but may provide higher compatibility with existing code.
	* This might allow, for instance, a relatively low-effort port of simple Unix-like software where a 'fake kernel' is provided.
* Which to use of `EBREAK` and Call-Into-Data depends entirely on the programming language you are using to write them.
	* In particular, it should be possible to express `EBREAK` syscalls via calls to the 'function' `{0x00100073, a, 0x00008067}` where `a` is the target string address.
		* _**This does not require inline assembly,**_ making it a good fallback option for 'uncooperative' compilers.
		* It introduces additional operations that aren't 'necessary', which may hamper speed, and the thunk 'functions' will occupy memory even if put in a zeroed section.
	* Call-Into-Data meanwhile requires forcing writing a `JAL` opcode and forcing the register setup/cleanup to go as expected. This might be more or less difficult in a given language.
		* `JAL` is a relatively hard-to-encode opcode, and the nature of relocations is unlikely to let you encode it via constant maths unless you know the string address extremely early.
		* **In practice, this requires inline assembly.**
		* Still, this is the best for performance; the impact on the data image is, in principle, the call instruction itself.
	* Using either is still infinitely better and more flexible than the `ECALL` method, especially as the backend may use syscalls as a gateway to inline target assembly.

Memory access during syscalls or between invocations is both possible and expected.

If the syscall may cause reentrancy, the following registers (the caller-saved registers) **must** be saved if necessary (as with any function call):

* `x1`/`ra`
* `x5`/`t0`
* `x6`/`t1`
* `x7`/`t2`
* `x28`/`t3`
* `x29`/`t4`
* `x30`/`t5`
* `x31`/`t6`
* While `a0`-`a7` are implicitly syscall arguments/return values, and thus should be _implicitly_ 'saved', if only some of them are used, they should still all be saved.

Notably, callee-saved registers should be reasonably safe as long as all entrypoints follow the RISC-V calling convention. If this isn't the case, then a proper context switch routine is required.

\* In principle, KIP32 could save data image bytes by using the compressed extension. The problem is that the amount of 'dummy' recompiled code would massively increase. \
For this reason, the instruction parsing apparatus was built around the idea that one word is one 'source instruction'. \
The `EBREAK` syscalls caused a few wasted instruction 'slots,' but allowed inlining syscalls, which ultimately improves reentrancy for the target. \
The `JAL` syscalls seem to solve many of the later issues in the project (Clang does _not_ like the inline assembly `EBREAK` syscalls were using). \
In addition, the architecture required for instruction fusion was added in order to support instructions where decoding needs reading outside of the instruction word.

## 'Known' Syscalls

These syscalls have fixed _intended_ meanings, and are thus reliable for libc.

Note that extremely niche cases may use special compiler flags to remove these syscalls.

* `stdsyscall_putchar`: Takes a single parameter in `a0`, no return value. Writes a single byte of debug output. The value 10/`\n` will flush the debug output implicitly.
	* Debug output is in an unspecified character set that should be at least compatible with ASCII for alphanumeric characters.
	* This is a maximum-reliability debug port and should not be used for end-user text unless otherwise specified.
* `stdsyscall_memmove`: Implements `memmove`. See appropriate C specification.
* `stdsyscall_sbrk`: Implements `sbrk`, minus effects on `errno` (since `errno`, if it exists, is internal).
	* This is intended to be used as a primitive to implement the `malloc` interface on top of. Therefore, if you're using a libc's `malloc`/`free`, you should not be using `sbrk` unless you know for a fact it's not calling this interface.
* `stdsyscall_getutctime`: Returns time since the Unix epoch in microseconds as a 64-bit number.
	* This means `a0` holds the lower bits and `a1` holds the upper bits.

## Regarding Spec Compliance

KIP32 is _intended to_ be spec-compliant for the processor that it is, much like some minimalist FPGA-grade RISC-V implementations.

That is, given the following assumptions:

1. Without implementing the privileged specification, the EEI is allowed to implement Requested Traps or Fatal Traps however it likes.
	* Citation: `20191213: 1.6 Exceptions, Traps, and Interrupts` (`How traps are handled and made visible to software running on the hart depends on the enclosing execution environment.`)
2. The EEI may define memory permissions arbitrarily, though _should_ define some area of memory as 'ordinary' read/write memory.
	* Citation: `20191213: 1.4 Memory` (`The execution environment determines what portions of the non-vacant address space are accessible for each kind of memory access.`)
	* Citation: `20191213: 1.4 Memory` (`it is usually expected that some portion will be specified as main memory.`)
3. Without implementing an instruction fence, the implementation may precache every valid instruction at the 'earliest opportunity', and as there is no instruction that can invalidate this cache, the implementation is allowed to make the cache read-only past that point.
	* Citation: `20191213: 1.4 Memory` (`avoid reading main memory for instruction fetches ever again`)
	* Citation: `20191213: "Zifencei" Instruction-Fetch Fence, Version 2.0` (`RISC-V does not guarantee that stores to instruction memory will be made visible to instruction fetches on a RISC-V hart until that hart executes a FENCE.I instruction.`)

And given the following conclusions:

1. Per assumption 1, it follows that following `EBREAK` with a non-executable address word is a permissible method of 'specifying' a desired effect.
	* A reasonably similar mechanism to the `EBREAK` syscall is used for RISC-V's semihosting ABI.
2. Per assumptions 1 and 2, it follows that a jump into any memory the EEI does not define as executable memory can cause behaviour defined by the EEI.
	* It follows that the EEI can choose to define this as undefined behaviour, or can choose to handle it in highly specific ways (i.e. Call-Into-Data).
3. Per assumption 3, it follows that if this memory is baked-in at design time, the earliest opportunity precedes the actual 'manufacturing' of the implementation, and so the prefetch may be baked-in also.
	* There are some particularly 'creative' ways to use this from a hardware perspective, but from a software perspective this allows AOT recompilation of sufficiently constrained RISC-V binaries.

Then KIP32 **should** (this is not a formally verified statement) be compliant with:

* `RV32I Base Integer Instruction Set, Version 2.1`
* `"M" Extension for Integer Multiplication and Division, Version 2.0`
	* (At least for the Udon target. If any other targets are written, they might not implement `M`.)

## Udon Supplement

The Udon target has ISA extensions: `M`.

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
* `builtin_asm:*`: The contents of `*` are parsed as a KU2 file.
	* This file is expected to act like a syscall package with the package marker stripped.

The syscall returns by `jump(_syscall_return)` or a sequence equivalent to:

```
copy_static(_syscall_return_indirect, vm_indirect_jump_target)
jump(_vm_indirect_jump)
```

Legacy `ecall` syscalls meanwhile call a single assembly routine. They set the indirect jump pointer for return to be possible, so either this must be properly backed up, or else reentrancy is not possible.

Note that it is reasonably trivial to write non-inlined nametable syscalls if reentrancy is not required for those syscalls, so the only reason to use the `ecall` syscall mechanism is with a precise and specific goal in mind.

Tighter integration may be achieved using:

* `--inc`; see transpiler help for details.
* See `sdk/stdsyscall.ron` and `sdk/include/kip32_udon.h`.

Finally, for debugging purposes, it's useful to know how to map internal Udon code addresses back to RISC-V addresses without a symbol table.

This can be achieved by iterating through the JUMP instructions at the start of the bytecode.

Viewed as a series of integers, this can be seen as a sequence of pairs of `5` followed by an instruction address, for each 32-bit instruction word starting from address 0.

Viewed as assembly, this tends to read as:

```
JUMP, _code_00000000
JUMP, _code_00000004
JUMP, _code_00000008
```

And so forth. The labels here show something important (that the addressing starts at zero and is sequential), but hide something else important (that this provides a lookup table).

Once a non-JUMP instruction is reached, the jump table is over; the rest of the contents of the program are still reserved by the transpiler to be "theoretically, anything".
