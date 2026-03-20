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

[working-directory: 'kip32']
kip32-libc:
	KIP32CC_OVERRIDE_ARCH=rv32i sdk/kip32-udon-gcc -O3 sdk/libc/memmove_syscall.S -c -o sdk/libc/memmove_syscall.o
	rm -f sdk/libc.a
	riscv64-unknown-elf-ar rcs sdk/libc.a sdk/libc/memmove_syscall.o

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
