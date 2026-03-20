build: udon kip32 mdbook

[working-directory: 'kip32']
kip32: kip32-libc
	cargo fmt
	cargo build
	cargo test -q

	riscv64-unknown-elf-gcc -mabi=ilp32 -march=rv32i -nostartfiles -nolibc -ffreestanding testing/qemu.S testing/muldiv.S testing/testlibc.c testing/genrefdata.c -o testing/genrefdata.elf
	KIP32CC_OVERRIDE_ARCH=rv32i sdk/kip32-udon-gcc -O3 testing/science.c -S -o testing/science.s
	KIP32CC_OVERRIDE_ARCH=rv32i sdk/kip32-udon-gcc -O3 testing/testlibc.c -S -o testing/testlibc.s
	KIP32CC_OVERRIDE_ARCH=rv32i sdk/kip32-udon-gcc -O3 testing/science.c testing/testlibc.c testing/muldiv.S -o testing/science.elf -Wl,-Map=testing/science.map
	sdk/elf2uasm testing/science.elf --ignore-emit-err -o ../kvassets/Assets/science.uasm
	sdk/elf2uasm testing/science.elf --udonjson -o ../kvassets/Assets/science.udonjson
	objdump -h -D testing/science.elf > testing/science.lst

[working-directory: 'kip32/sdk/libc']
kip32-libc:
	rm -f obj/*.o
	../kip32-udon-gcc -O3 stdlib/abort.S -c -o obj/abort.o
	../kip32-udon-gcc -O3 stdlib/abs.S -c -o obj/abs.o
	../kip32-udon-gcc -O3 stdlib/bsearch.c -c -o obj/bsearch.o
	../kip32-udon-gcc -O3 stdlib/div.c -c -o obj/div.o
	../kip32-udon-gcc -O3 stdlib/ldiv.c -c -o obj/ldiv.o
	../kip32-udon-gcc -O3 stdlib/llabs.c -c -o obj/llabs.o
	../kip32-udon-gcc -O3 stdlib/lldiv.c -c -o obj/lldiv.o
	../kip32-udon-gcc -O3 stdlib/qsort.c -c -o obj/qsort.o
	../kip32-udon-gcc -O3 stdlib/system.S -c -o obj/system.o
	../kip32-udon-gcc -O3 errno.c -c -o obj/errno.o
	../kip32-udon-gcc -O3 fread.c -c -o obj/fread.o
	../kip32-udon-gcc -O3 fwrite.c -c -o obj/fwrite.o
	../kip32-udon-gcc -O3 memmove_syscall.S -c -o obj/memmove_syscall.o
	rm -f ../libc.a
	riscv64-unknown-elf-ar rcs ../libc.a obj/*.o

[working-directory: 'mdbook']
mdbook:
	./b

[working-directory: 'udon']
udon:
	cargo fmt
	cargo build
	cargo test -q

# specialized

[working-directory: 'kip32']
kip32-idec:
	cargo fmt
	KIP32_TEST_EXHAUSTIVE=1 cargo test -- --show-output

[working-directory: 'kvtools/Editor']
datamine2json:
	./datamine2json.py
