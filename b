#!/bin/sh
set -e
cargo fmt
cargo build
cargo test
cd kip32
sdk/kip32cc -O3 science.c -o science.elf
sdk/elf2uasm science.elf -o ../kvassets/Assets/science.uasm
objdump -h -D science.elf > science.lst
