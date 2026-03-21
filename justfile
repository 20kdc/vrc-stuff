build: udon kip32 mdbook

[working-directory: 'kip32']
kip32: kip32-libc
	cargo fmt
	cargo build
	cargo test -q

	riscv64-unknown-elf-gcc -specs=sdk/libcqemu.specs -mabi=ilp32 -march=rv32i -ffreestanding testing/libctest.c testing/qemu.S testing/testlibc.c -o testing/libctest.elf
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
	../recipe "../kip32-udon-gcc -O3" "-c -o" "" `cat objects.txt`
	rm -f ../libc.a
	riscv64-unknown-elf-ar rcs ../libc.a obj/*.o
	riscv64-unknown-elf-ar rcs ../libcqemu.a `cat qemuobj.txt`

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
