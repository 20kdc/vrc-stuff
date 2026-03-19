build: udon kip32 mdbook

[working-directory: 'kip32']
kip32:
	cargo fmt
	cargo build
	cargo test -q

	riscv64-unknown-elf-gcc -mabi=ilp32 -march=rv32i -nostartfiles -nolibc -ffreestanding testing/qemu.S testing/muldiv.S testing/genrefdata.c -o testing/genrefdata.elf

	sdk/kip32cc -O3 testing/science.c -S -o testing/science.s
	sdk/kip32cc -O3 testing/science.c testing/muldiv.S -o testing/science.elf -Wl,-Map=testing/science.map
	sdk/elf2uasm testing/science.elf --ignore-emit-err -o ../kvassets/Assets/science.uasm
	sdk/elf2uasm testing/science.elf --udonjson -o ../kvassets/Assets/science.udonjson
	objdump -h -D testing/science.elf > testing/science.lst

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
