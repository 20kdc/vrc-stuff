#include "qemu.h"
#include "testlibc.h"
#include <stdlib.h>
#include <string.h>
#include <assert.h>

void putchar(int chr) {
	write(1, &chr, 1);
}

void __assert_fail(const char * exprtext, const char * file, int line, const char * func) {
	putsn("assertion failed: ");
	putsn(exprtext);
	putsn(" @ ");
	putsn(file);
	putsn(":");
	puthex(line);
	putsn(" (");
	putsn(func);
	puts(")");
	abort();
}

#define STRTOL_TEST_S(v, p) \
	if (strtol(#v, NULL, p) != v) { \
		puts("strtol: " #v " " #p " fail"); \
	} else { \
		puts("strtol: " #v " " #p " OK"); \
	}

#define STRTOL_TEST(v, p) \
	STRTOL_TEST_S(v, p) \
	STRTOL_TEST_S(-v, p)

void _start() {
	puts("libctest started!");

	STRTOL_TEST(1234, 10);
	STRTOL_TEST(1234, 0);
	STRTOL_TEST_S(+1234, 0);
	STRTOL_TEST(01234, 8);
	STRTOL_TEST(01234, 0);
	STRTOL_TEST(0x10000, 16);
	STRTOL_TEST(0x10000, 0);
	STRTOL_TEST(0xFFFFFFFF, 16);
	STRTOL_TEST(2147483647, 10);
	STRTOL_TEST_S(-2147483648, 10);

	puts("performing rand tests...");
/*
import java.util.Random;
class Main {
        public static void main(String[] s) {
                Random r = new Random();
                r.setSeed(0);
                for (int i = 0; i < 4; i++) {
                        int a = r.nextInt();
                        System.out.println(a & 0x7FFFFFFF);
                }
        }
}
*/
	srand(0);
	assert(rand() == 991999072);
	assert(rand() == 1423528248);
	assert(rand() == 1033096058);
	assert(rand() == 456749246);

	puts("performing strlen tests...");

	assert(strlen("") == 0);
	assert(strlen("a") == 1);
	assert(strlen("aa") == 2);
	assert(strlen("aaa") == 3);
	assert(strlen("aaaa") == 4);
	assert(strlen("aaaaa") == 5);

	puts("hey, all tests completed!");

	_exit(0);
}
