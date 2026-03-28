# kip32 SDK

The kip32 SDK is a setup-in-place package containing most things necessary to write Udon behaviours in C.

Run `./configure` on a Unix-like (only Linux tested) or on MSYS2 (untested, but soon!).

This script:

* Sets up convenience scripts such as `kip32-udon-gcc`
* (Re)compiles the libc
* TODO: Cargo-compile `elf2uasm` and `kip32corestub` and copy them into the SDK directory, ready for use.
* TODO: Once the details of a Rust SDK are figured out, copy those in too.

The result is a ready-to-use environment.
