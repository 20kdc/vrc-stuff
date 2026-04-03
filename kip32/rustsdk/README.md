# kip32 Rust SDK

The kip32 Rust SDK is like the regular kip32 SDK, and pulls in its linker script.

However, being pure-Rust, it can be used without MSYS2 (though it may be convenient for debugging).

To use the kip32 Rust SDK, you should compile and perhaps `cargo install` (or otherwise have available) these tools:

* `kip32-elf2uasm`
* `kip32-corestub`

Unlike with the C version of the SDK, the QEMU target is not planned for.

Be aware of potential licensing considerations around:

* The Rust Standard Library itself
* <https://github.com/rust-lang/compiler-builtins>
