#!/bin/sh
set -e
cargo fmt
cargo build
cargo test
cd kip32
sdk/kip32cc -O3 science.c -S -o science.s
sdk/kip32cc -O3 science.c -o science.elf -Wl,-Map=science.map
sdk/elf2uasm science.elf --ignore-emit-err -o ../kvassets/Assets/science.uasm
sdk/elf2uasm science.elf --udonjson -o ../kvassets/Assets/science.udonjson
objdump -h -D science.elf > science.lst
