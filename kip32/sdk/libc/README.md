## public-domain partial libc

This is being written out of frustration with the rather large picolibc copyright file (and the issues it causes when TextMeshPro is exposed to it).

It is massively incomplete, and heavily untested, but is 100% public-domain code.

It is likely to _**never**_ support floating-point calculations.

## Quirks

* Floats do not exist in this universe. Anything that involves floats is not supported.
	* Floating-point code is a significant quantity of the copyright notices in other libc implementations.
		* The basic approach that seems common is to copy the Sun Microsystems trigonometry functions. \
		  What's really funny is that some call the responsible company SunPro and some SunSoft. \
		  Licensing error, or confusing business structure? You decide!
* Wide character, unicode multi-byte etc. is out of scope
	* These functions have been defined poorly for years, nobody uses them unless forced, etc.
* `__assert_fail` is left intentionally declared but unimplemented.
* `FILE` is not opaque, but the structure is incomplete for a full stdio implementation.
	* Most file functions are left intentionally unimplemented.
	* Functions that are implemented in the libc only rely on fields in the given structure.
	* This design allows `fprintf`/`fscanf` to be generic.
* `system` is used to output debug messages. It calls `stdsyscall_putchar`.
	* This is technically a valid implementation, because I say that's how the command processor works on this system.
* `rand` and `srand` are implemented using the `java.util.Random` algorithm.
* It is expected that when `time.h` is implemented, it will exist in a permanent UTC timezone.
* Like many libcs before it, the libc is usually split one-file-per-function so that only necessary objects are linked.

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

* `assert.h` (status: complete -- intentional 'DIY handler' quirk)
* `ctype.h` (status: complete)
* `errno.h` (status: complete)
* `inttypes.h` (status: complete -- least/fast SCN macros are 'too unknown' so haven't been implemented)
* `locale.h` (status: complete -- fixed single locale)
* `math.h` (status: intentional stub)
* `setjmp.h` (status: complete)
* `signal.h` (status: intentional stub)
* `stdio.h` (status: in-progress)
* `stdlib.h` (status: in-progress)
* `string.h` (status: in-progress)
* `time.h` (status: stub-ish)
