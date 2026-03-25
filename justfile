build: udon kip32 mdbook

[working-directory: 'kip32']
kip32: kip32-libc
	cargo fmt
	cargo build
	cargo test -q

	sdk/kip32-libcqemu-gcc -g -O3 testing/muldiv.S testing/qemu_stdio.c testing/genrefdata.c -o testing/genrefdata.elf
	KIP32CC_OVERRIDE_ARCH=rv32i sdk/kip32-udon-gcc -O3 testing/science.c -S -o testing/science.s
	KIP32CC_OVERRIDE_ARCH=rv32i sdk/kip32-udon-gcc -O3 testing/science.c testing/muldiv.S -o testing/science.elf -Wl,-Map=testing/science.map
	sdk/elf2uasm testing/science.elf --ignore-emit-err -o ../kvassets/Assets/science.uasm
	sdk/elf2uasm testing/science.elf --udonjson -o ../kvassets/Assets/science.udonjson
	# if this gives you an error, you probably need binutils-multiarch
	objdump -h -D testing/science.elf > testing/science.lst || true

[working-directory: 'kip32/testing']
kip32-libc-test: kip32-libc
	./libctest.elf

[working-directory: 'kip32/testing']
kip32-libc-test-gdb: kip32-libc
	echo '> gdb-multiarch -ex "target remote localhost:8192"'
	qemu-riscv32-static -g 8192 ./libctest.elf

[working-directory: 'kip32/sdk/libc']
kip32-libc:
	rm -f obj/*.o obj_specific/*.o
	../recipe "../kip32-udon-gcc -g -O3" "-c -o" "" `cat objects.txt`
	rm -f ../libcudon.a ../libcqemu.a
	riscv64-unknown-elf-ar rcs ../libcudon.a obj/*.o obj_specific/system_putchar.o obj_specific/memmove_syscall.o obj_specific/sbrk_kip32.o
	riscv64-unknown-elf-ar rcs ../libcqemu.a obj/*.o obj_specific/memmove_slow.o obj_specific/linux_syscalls.o obj_specific/sbrk_fake.o
	../kip32-libcqemu-gcc -g -O3 ../../testing/libctest.c ../../testing/qemu_stdio.c -o ../../testing/libctest.elf

[working-directory: 'kip32/testing']
libc-host-weird:
	cc test_weird.c -o test_weird
	./test_weird

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
