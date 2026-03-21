#include "qemu.h"
#include "testlibc.h"
#include <stdlib.h>

void putchar(int chr) {
	write(1, &chr, 1);
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
	STRTOL_TEST(+1234, 0);
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
	int randTestOk = 1;
	randTestOk &= rand() == 991999072;
	randTestOk &= rand() == 1423528248;
	randTestOk &= rand() == 1033096058;
	randTestOk &= rand() == 456749246;
	if (randTestOk) {
		puts("rand test OK");
	} else {
		puts("rand test FAIL");
	}

	_exit(0);
}
