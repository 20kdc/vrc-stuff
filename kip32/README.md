# KIP32: RV32IM to Udon transpiler

Ok, so, basically, I remembered something I heard about static recompilation.

And then I realized, most of the issues don't apply if the user trusts their own code not to do JIT or anything weird!

**Therefore, this project statically recompiles RV32IM instructions into Udon.**

It can compile to Udon Assembly or to `udonjson` (see `kvtools`).

## Why would you want to do that?

Writing complex logic in C/Rust/etc. targetting VRChat.

## Why not use WebAssembly/LLVM bitcode/etc.?

All of these options make various mechanisms _the implementor's problem._

As the complexity of this project shows, the implementor really can't afford to have any _more_ complexity if they want to get whatever their actual goal is completed in a timely manner.

Exception handling, for instance, would be a total mess. It's better implemented in-VM.

Firmly not-helping is that the WebAssembly 3.0 core spec includes things like _vector types._ This is not viable.

Meanwhile, RV32IM has a clear minimal set of instructions a compiler can be told to target. Is it optimal? No, but it also avoids some nasty pitfalls.

* Large register count is good for performance of resulting Udon code.
	* Since the compiler is managing spilling and saving registers in as optimal a way as possible, we don't have to try doing it ourselves (but worse).
* ISA has good tooling support even in minimal configurations (soft-float, etc.)

## How does it work from the behaviour programmer's view?

1. Maybe continue to write code that heavily interacts with Unity in Udon Graph or UdonSharp.
	* Any of this code that you write, you'll need to figure out how to emulate.
2. Write _complex_ code in C/Rust/etc.
	* Write this code as if you're writing it for a microcontroller you don't happen to have on your desk. \
	  In other words, you should have a clear method of testing as a native executable.
	* The `kip32.h` header comes in both on-host and in-Udon variants.
3. Compile to what is essentially a RV32IM microcontroller. Link with `sdk/kip32.ld` linker script.
	* The `sdk/kip32-udon-gcc` script is intended to be a convenient frontend.
	* Alternatively, if you want things like 'an actual libc' you might want i.e:
		* `picolibc-riscv64-unknown-elf`
		* `-mabi=ilp32 -march=rv32im -I$(KIP32_SDK)/include -nostartfiles -specs=/usr/lib/picolibc/riscv64-unknown-elf/picolibc.specs`
			* the `-nostartfiles` is because `_start` is both a libc function name and an Udon event name, which may be good or bad depending on how you look at it
		* and other such Fun Stuff
	* Read `ABI.md` for how the interface works.
	* Since the M extension is implemented, this has probably removed most encounters you'll have with `libgcc`/`compiler-rt` for 'normal' code. Still, be aware they exist.
4. Tighter integration may be achieved using various flags, particularly `--inc`; see transpiler help for details.
	* Also see `sdk/stdsyscall.ron`.
	* You can use the `udonjson` output format in order to use constants not supported by Udon Assembly.
5. Udon Assembly doesn't play as well as it could with import on the no-auto-import configuration.
	* For this reason, you may have to manually delete the SerializedUdonProgram file to get it to recompile.
	* Use `udonjson` to avoid this.
6. For debugging, you have multiple choices (ideally use whatever fits):
	* You can debug an in-Unity Udon error, including inspection of all RISC-V variables, by:
		1. Attaching the `kvtools` Udon debug options component onto the offending GameObject.
			* This is safe to 'leave on' unless it conflicts with some other middleware you're using.
		2. Causing the error to happen.
		3. Press `Dump Error To File`, save the file somewhere.
		4. Run `kip32corestub`, passing it the file. This will give you a 'GDB stub' you can target with GDB, or typical embedded debugging tools (that inevitably wrap GDB).
	* You can use the QEMU variant of the libc to build simple command-line applications for testing and debugging.
		* The QEMU variant of the libc makes the arguably tenuous assumption you have access to a working Linux userland-emulating (not `-system-`) QEMU.
			* This may be awkward on some Windows variants.
	* You can write and build applications outside KIP32 entirely that share the 'difficult' code with the KIP32 version.

## Future extensions?

* Floating-point support would be nice, and may be added in future, but spec compliance is guaranteed to fail.
	* Instruction RM flag will be ignored, and `fcsr` simply won't exist. However, the performance boost will be worth it for some applications, and the compiler can be told not to use it.

## Why Udon Assembly, despite its issues re: constants?

**At this point, it's basically just so that deployment is zero-C#.**

The new assembly has been deployed. The C# part of it seems to tick over decently.

Still, the issues with constants don't actually severely interfere with RISC-V dynamic recompilation _itself_ because we don't need constants of the types affected by those issues.

In fact, as it turns out, there are so many flaws in Udon's handling of numeric types that you basically should use as high-precision a type as you dare _whenever possible,_ just so you don't have to AND off bits, just so that `System.Convert` won't come knocking with an exception.

RV32IM only performs type conversions during loads and stores. It is no coincidence that the load/store code is the most painful part of the recompiler.

## Structure

* `kip32ingest`: Reads RISC-V code, handles ELF reading, instruction fusion, ABI things.
* `elf2uasm`: Converts a RISC-V ELF into Udon Assembly (or `.udonjson` but that was added later)
* `elf2uasm_lib`: This is where stuff that `elf2uasm` and `kip32corestub` need to both know is stored.
* `kip32corestub`: Program intended to allow examining a kip32 Udon coredump using i.e. `gdb-multiarch`.
	* This program relies heavily on <https://github.com/daniel5151/gdbstub>.
* `micropython`: Contains the MicroPython port code.
* `sdk`: Contains 'SDK' code, such as the libc.
* `testing`: Contains test code to confirm things work.
