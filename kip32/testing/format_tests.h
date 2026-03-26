#include <assert.h>
#include <stdio.h>
#include <string.h>

void stdio_tests() {
	printf("fmt: num %i, %08d, %-8.3x, '%-8.3i', %#xc, %#o\n", 1234, 12, -1, -1, 16, 0755);
	printf("fmt: word: '%08s' '%-08s' '%8s' '%-8s', %.5s\n", "bun", "bun", "bun", "bun", "truncateme");
	// in case you're wondering, use "0x%08x" instead.
	// with %#08x, it will pad over the 0x with zeroes if the value is zero
	printf("fmt: glibc and kip32 libc agree, %#08x is not how you do this. really, don't: %#08x\n", 0x1234, 0);

	int tap1 = -1, tap2 = -1, tap3 = -1, tap4 = -1, tap5 = -1;
	int a = -1, b = -1, c = -1, d = -1, e = -1, f = -1, g = -1, count = -1;
	count = sscanf("1234 5678 -8765 -4321 -0x100 0x80000000 -0x80000000", "%n%i%n %n%i%n %i %i %i %i %i%n", &tap1, &a, &tap2, &tap3, &b, &tap4, &c, &d, &e, &f, &g, &tap5);
	printf("scanf: count: %i t1:%i a:%i t2:%i t3:%i b:%i t4:%i c:%i d:%i e:%i f:%i g:%i t5:%i\n", count, tap1, a, tap2, tap3, b, tap4, c, d, e, f, g, tap5);
	assert(tap1 == 0);
	assert(a == 1234);
	assert(tap2 == 4);
	assert(tap3 == 5);
	assert(b == 5678);
	assert(tap4 == 9);
	assert(c == -8765);
	assert(d == -4321);
	assert(e == -0x100);
	assert(f == -0x80000000);
	assert(g == -0x80000000);
	assert(tap5 == 51);

	sscanf("1234567 1234567", "%o %2o%o", &a, &b, &c);
	printf("scanf: %o %o %o\n", a, b, c);
	assert(a == 01234567);
	assert(b == 012);
	assert(c == 034567);

	char strA[32] = {};
	char strB[32] = {};
	char strC[32] = {};
	char strD[32] = {};
	// gets interrupted -- a useful test case
	count = sscanf("toki jan Ketesi o", " %[aeijklmnopstuw] %[aeijklmnopstuw] %[aeijklmnopstuw] %[AEIJKLMNOPSTUWaeijklmnopstuw ]", strA, strB, strC, strD);
	printf("scanf: %i %s|%s|%s|%s\n", count, strA, strB, strC, strD);
	assert(count == 2);
	assert(!strcmp(strA, "toki"));
	assert(!strcmp(strB, "jan"));
	assert(!strcmp(strC, ""));
}
