#include <stdio.h>

void stdio_tests() {
	printf("fmt: num %i, %08d, %-8.3x, '%-8.3i', %#xc, %#o\n", 1234, 12, -1, -1, 16, 0755);
	printf("fmt: word: '%08s' '%-08s' '%8s' '%-8s', %.5s\n", "bun", "bun", "bun", "bun", "truncateme");
	// in case you're wondering, use "0x%08x" instead.
	// with %#08x, it will pad over the 0x with zeroes if the value is zero
	printf("fmt: glibc and kip32 libc agree, %#08x is not how you do this. really, don't: %#08x\n", 0x1234, 0);
}
