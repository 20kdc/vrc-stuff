build: udon kip32 site

[working-directory: 'kip32']
kip32: kip32-sdk kip32-sdk-examples kip32-rust-sdk
	sdk/bin/kip32-libcqemu-gcc -g -O3 testing/muldiv.S testing/qemu_stdio.c testing/genrefdata.c -o testing/genrefdata.elf
	KIP32CC_OVERRIDE_ARCH=rv32i sdk/bin/kip32-udon-gcc -O3 testing/science.c -S -o testing/science.s
	KIP32CC_OVERRIDE_ARCH=rv32i sdk/bin/kip32-udon-gcc -O3 testing/science.c testing/muldiv.S -o testing/science.elf -Wl,-Map=testing/science.map
	sdk/bin/kip32-elf2uasm testing/science.elf --ignore-emit-err -o ../kvassets/Assets/science.uasm
	sdk/bin/kip32-elf2uasm testing/science.elf --udonjson -o ../kvassets/Assets/science.udonjson
	# if this gives you an error, you probably need binutils-multiarch
	objdump -h -D testing/science.elf > testing/science.lst || true

[working-directory: 'kip32/rustsdk']
kip32-rust-sdk: kip32-tools
	cargo fmt
	cargo build

[working-directory: 'kip32/tools']
kip32-tools:
	cargo fmt
	cargo build
	cargo test -q

[working-directory: 'kip32/testing']
kip32-libc-test: kip32-sdk
	./libctest.elf

[working-directory: 'kip32/testing']
kip32-libc-test-gdb: kip32-sdk
	echo '> gdb-multiarch -ex "target remote localhost:8192"'
	qemu-riscv32-static -g 8192 ./libctest.elf

[working-directory: 'kip32/sdk']
kip32-sdk: kip32-tools
	./configure
	./bin/kip32-libcqemu-gcc -g -O3 ../testing/libctest.c ../testing/qemu_stdio.c -o ../testing/libctest.elf

[working-directory: 'kip32/sdk/examples']
kip32-sdk-examples: kip32-sdk
	./build

[working-directory: 'kip32/testing']
libc-host-weird:
	cc test_weird.c -o test_weird
	./test_weird

[working-directory: 'site']
site:
	./b

[working-directory: 'site']
site-srv: site
	miniserve out

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

[working-directory: 'kvresearch/Editor']
datamine2json:
	./datamine2json.py

clean:
	cd kip32/sdk ; ./clean
	cargo clean --manifest-path site/generator/Cargo.toml
	cargo clean --manifest-path udon/Cargo.toml

kip32-sdk-src-package: clean
	zip kip32-sdk-src-package.zip -r kip32/sdk kip32/tools udon
