build: udon kip32 mdbook

[working-directory: 'kip32']
kip32:
	cargo fmt
	cargo build
	cargo test -q

	sdk/kip32cc -O3 science.c -S -o science.s
	sdk/kip32cc -O3 science.c -o science.elf -Wl,-Map=science.map
	sdk/elf2uasm science.elf --ignore-emit-err -o ../kvassets/Assets/science.uasm
	sdk/elf2uasm science.elf --udonjson -o ../kvassets/Assets/science.udonjson
	objdump -h -D science.elf > science.lst

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
