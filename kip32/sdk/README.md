# kip32 SDK

The kip32 SDK is a setup-in-place package containing most things necessary to write Udon behaviours in C.

## Requirements

TLDR: You need a 'sufficiently' Unix-like system with Rust and `riscv64-unknown-elf-gcc`.

If you're on Windows, get MSYS2 <https://www.msys2.org/>, **run it in `MSYS2 MINGW64` mode**, and install these packages:

```
pacman -S mingw-w64-x86_64-riscv64-unknown-elf-gcc
pacman -S rust
# optional:
pacman -S gdb-multiarch
```

_The packages may be installed from the default UCRT64 mode, but they won't be usable until you switch._

_It may also be helpful to install a C compiler of your choice for building 'regular' code._

On operating systems other than Windows, the above package list is still a good guide (minus the `mingw-w64-x86_64-` prefix), but you should [install Rust normally.](https://rust-lang.org/tools/install/)

In addition, Linux users in particular may benefit from installing QEMU 'user-space emulation', which allows them to run `libcqemu` binaries.

## Installation Guide

Once you have MSYS2 or a sufficiently prepared Unix-like terminal:

Run `./configure` on a Unix-like (only Linux tested) or on MSYS2 (untested, but soon!).

This script:

* Sets up convenience scripts such as `kip32-udon-gcc`
* (Re)compiles the libc
* TODO: Cargo-compile `elf2uasm` and `kip32corestub` and copy them into the SDK directory.
	* The goal here is that we should have a clear path to the existence of pre-configured SDK releases.
* TODO: Once the details of a Rust SDK are figured out, the SDK should contain that.

The result is a ready-to-use environment.

## Usage

The kip32 SDK is structurally similar to open SDKs for embedded devices.

This is to say, it has:

1. A compiler 'frontend' script, `kip32-udon-gcc`, which provides critical basic compiler options for targetting the 'device', creating ELF files.
2. Supporting libraries such as the `libc`.
3. An ELF conversion tool, `elf2uasm`, which converts from the compiled ELF file to a suitable for the 'device' (Udon Assembly or `.udonjson`).
4. 'Write and debug' infrastructure, such as `kvtools` (allows `.udonjson` import and retrieving coredumps) and `kip32corestub` (allows debugging said coredumps).

The kip32 SDK tries not to decide how you choose to manage your build.

However, a simple program compilation flow might look something like:

```
# compile to ELF
kip32-udon-gcc program.c -o program.elf
# convert to Udon Assembly
elf2uasm program.elf -o program.uasm
# program.uasm is ready for immediate use
```

MSYS2 allows the use of Make, CMake, and other typical supporting infrastructure.
