## public-domain partial libc

This is being written out of frustration with the rather large picolibc copyright file (and the issues it causes when TextMeshPro is exposed to it).

It is somewhat incomplete, and heavily untested, but is 100% public-domain code.

It is likely to _**never**_ support floating-point calculations.

It can (somehow) compile MicroPython (likely owing to it using very few libc functions). (I thought I saw it running, but it might have been an outdated build owing to Udon program refresh quirks that I hadn't yet addressed.)

## Quirks

This is not a general-purpose libc. It can _just about_ survive in QEMU, which is important for quick testing.

* Floats do not exist in this universe. Anything that involves floats is not supported.
	* Floating-point code is a significant quantity of the copyright notices in other libc implementations.
		* The basic approach that seems common is to copy the Sun Microsystems trigonometry functions. \
		  What's really funny is that some call the responsible company SunPro and some SunSoft. \
		  Licensing error, or confusing business structure? You decide!
* Wide character, unicode multi-byte etc. is out of scope
	* These functions have been defined poorly for decades, nobody uses them unless forced, etc.
* `__assert_fail` should be replacable and uses the 'de-facto standard' API shared between glibc and musl.
* `FILE` is not opaque, but the structure is incomplete for a full stdio implementation.
	* Most file functions are left intentionally unimplemented.
	* Functions that are implemented in the libc only rely on fields in the given structure.
	* This design allows `vfprintf`/`vfscanf` to be the 'primary' formatting functions, with all other functions just being wrappers.
	* `__kip32_libc_buffile`, `__kip32_libc_charfile_read`, and `__kip32_libc_charfile_write` are given as utility functions.
* `system` is used to output debug messages. It calls `stdsyscall_putchar`.
	* This is technically a valid implementation, because I say that's how the command processor works on this system.
	* `__kip32_libc_systemout` is a `FILE` that calls `system` for writes and fails reads.
* `rand` and `srand` are implemented using the `java.util.Random` algorithm.
* `itoa` and friends are considered 'canon' in this libc.
	* This has to do with how important they are as utility functions for implementing printf/etc.
* `strtol` and friends do not check for over/underflow.
* `aligned_alloc` does not exist, on purpose (it would massively complicate malloc)
* `SCN*LEAST` and `SCN*FAST` are not implemented because we don't know _exactly_ which type the compiler will use.
* Anything relating to `[u]intmax_t` is assumed to be `[unsigned] long long`. Symbol aliasing is used here.
* The following routines use 'non-traditional' optimization practices:
	* `memcpy`/`memmove` have a relatively fast kip32 version (but are still going to be slow on QEMU).
		* For practical purposes, you should consider these to be _constant-time._ That is, not even `O(N)`. Just `O(1)`.
	* `memset` takes advantage of the `memcpy` speed.
	* `strlen`'s implementation favours the Udon environment heavily.
	* `qsort` is not quicksort, it is binary insertion sort. It allocates size for one element on stack.
		* This at least does not involve hand-assembled code. You could theoretically drop this into any libc and it should work.
		* This is optimized for the Udon target, i.e. `memmove` is incredibly fast and comparisons are incredibly slow.
* Non-standard constants are defined for accessing `jmp_buf` contents.
	* This is rather useful if you're implementing something (i.e. threading) that might wish to setup custom `jmp_buf` objects.
* It is expected that when `time.h` is implemented, it will exist in a permanent UTC timezone.
* Like many libcs before it, the libc is usually split one-file-per-function so that only necessary objects are linked.
* This libc has no startfiles. For related reasons, `--no-relax-gp` is specified, since otherwise QEMU runs into issues.
	* It's also specified for non-QEMU targets as well. In the context of kip32, any benefit can be pretty much eliminated using better instruction fusion.

## Headers / Requirements

This libc assumes a modern compiler. In specific, it's written for riscv64-unknown-elf-gcc and literally nothing else.

This libc assumes your toolchain provides i.e. `/usr/lib/gcc/riscv64-unknown-elf/*/include/` containing GCC Runtime Library Exception-covered headers.

This matters in particular for:

* `float.h`
* `iso646.h`
* `limits.h`
* `stdalign.h`
* `stdarg.h`
* `stdatomic.h`
* `stdbool.h`
* `stddef.h`
* `stdfix.h` (something to do with a ratified extension)
* `stdint.h`
* `stdnoreturn.h`
* `tgmath.h` (no idea but C11 says it's a thing and it exists in libgcc)

This leaves the following headers up for implementation:

* `assert.h`
* `complex.h`
* `ctype.h`
* `errno.h`
* `fenv.h`
* `inttypes.h`
* `locale.h`
* `math.h`
* `setjmp.h`
* `signal.h`
* `stdio.h`
* `stdlib.h`
* `string.h`
* `threads.h`
* `time.h`
* `uchar.h`
* `wchar.h`
* `wctype.h`

Of which are in-scope:

* `assert.h` (status: complete)
* `ctype.h` (status: complete)
* `errno.h` (status: complete)
* `inttypes.h` (status: complete -- least/fast SCN macros are 'too unknown' so haven't been implemented)
* `locale.h` (status: complete -- fixed single locale)
* `math.h` (status: intentional stub)
* `setjmp.h` (status: complete)
* `signal.h` (status: intentional stub)
* `stdio.h` (status: scan needs testing, otherwise complete)
* `stdlib.h` (status: complete)
* `string.h` (status: complete)
* `time.h` (status: incomplete -- no `mktime` or `strftime`)

