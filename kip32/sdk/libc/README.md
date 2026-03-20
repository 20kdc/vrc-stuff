## public-domain partial libc

This is being written out of frustration with the rather large picolibc copyright file (and the issues it causes when TextMeshPro is exposed to it).

It is massively incomplete, and heavily untested, but is 100% public-domain code.

It is likely to _**never**_ support floating-point calculations.

It assumes your toolchain provides i.e. `/usr/lib/gcc/riscv64-unknown-elf/*/include/` containing GCC Runtime Library Exception-covered headers.

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

* `assert.h`
* `ctype.h`
* `errno.h` _with caveats_
* `inttypes.h`
* `locale.h` _with caveats_ (facade)
* `math.h` (dummy file)
* `setjmp.h`
* `signal.h`
* `stdio.h`
* `stdlib.h` _with caveats_ (no real OS)
* `string.h`
* `time.h`
* `uchar.h`
* `wchar.h`
* `wctype.h`

## Quirks

* `system` is used to output debug messages. It calls `stdsyscall_putchar`. This is technically a valid implementation, because I say that's how the command processor works.
