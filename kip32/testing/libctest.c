#include <unistd.h>
#include <errno.h>
#include <locale.h>
#include "tiolib.h"
#include <stdlib.h>
#include <string.h>
#include <assert.h>

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

void errno_tests();
void format_tests();
void itoa_tests();
void locale_tests();
void stdio_tests();
void stdlib_tests();
void malloc_tests();
void string_tests();
void time_tests();
void ctype_tests();
void setjmp_tests();

#define TF_RUNMODULE(mod) \
	puts("running " #mod " tests..."); \
	mod ## _tests();

void _start() {
	puts("libctest started!");

	TF_RUNMODULE(errno);
	TF_RUNMODULE(format);
	TF_RUNMODULE(itoa);
	TF_RUNMODULE(locale);
	TF_RUNMODULE(stdio);
	TF_RUNMODULE(stdlib);
	TF_RUNMODULE(malloc);
	TF_RUNMODULE(string);
	TF_RUNMODULE(time);
	TF_RUNMODULE(ctype);
	TF_RUNMODULE(setjmp);

	puts("using sbrk to allocate data...");

	int * newdata = sbrk(4096);
	assert(newdata);
	assert(newdata[0] == 0);

	puts("hey, all tests completed!");

	_exit(0);
}

void errno_tests() {
	assert(!strcmp(strerror(ENOMEM), "Out of memory"));
	assert(!strcmp(strerror(EDOM), "Numerical argument out of domain"));
	assert(!strcmp(strerror(ERANGE), "Argument out of range"));
	assert(!strcmp(strerror(EILSEQ), "Invalid or incomplete multibyte or wide character"));
	assert(!strcmp(strerror(0), "Unknown error number"));
}

void format_tests() {

}

void itoa_tests() {
	char itoa_buf[__KIP32_LIBC_ITOA_BUFSIZE];
#define ITOA_TEST(f, v, r, s) f(v, itoa_buf, r); \
	if (strcmp(itoa_buf, s)) { system("itoa error "); puts(itoa_buf); } \
	assert(!strcmp(itoa_buf, s));
	ITOA_TEST(itoa, 528491, 10, "528491");
	ITOA_TEST(itoa, -528491, 10, "-528491");
	ITOA_TEST(itoa, -0xABCD, 16, "-abcd");
	ITOA_TEST(itoa, 0xABCD, 16, "abcd");
	/* the really nasty ones */
	ITOA_TEST(itoa, -0x80000000, 16, "-80000000");
	ITOA_TEST(uitoa, 0x80000000, 16, "80000000");
}

void locale_tests() {
	assert(localeconv());
	setlocale(LC_ALL, "C");
}

void stdio_tests() {
	/* is there even a good way to test this...? this is more format's thing */
}

void stdlib_tests() {

	// -- strtol --

#define STRTOL_TEST_S(v, p) assert(strtol(#v, NULL, p) == v);

#define STRTOL_TEST(v, p) \
	STRTOL_TEST_S(v, p) \
	STRTOL_TEST_S(-v, p)

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

	// -- rand --

	srand(0);
	assert(rand() == 991999072);
	assert(rand() == 1423528248);
	assert(rand() == 1033096058);
	assert(rand() == 456749246);
}

void malloc_tests() {
	// -- malloc --
	// this test rig is meant to produce a lot of reclaims
	int mallocLoud = 0;
	void * ptrs[0x40];
	for (int i = 0; i < 0x40; i++) {
		ptrs[i] = NULL;
	}
	for (int i = 0; i < 0x400; i++) {
		int fop = rand();

		// make things fuzzy
		int sz = fop & 0xF;
		fop >>= 4;
		// shift up to 24
		int szB2 = fop & 0xF;
		fop >>= 4;
		szB2 += fop & 0x7;
		fop >>= 3;
		sz <<= szB2;
		// make things fuzzier
		sz += fop & 0xF;
		fop >>= 4;

		int idx = fop & 0x3F;
		fop >>= 6;

		int coin = fop & 1;
		fop >>= 1;

		if (coin)
			sz = 0;
		if (!sz) {
			if (mallocLoud) {
				putsn(" free ");
				puthex(idx);
				putsn("\n");
			}
			free(ptrs[idx]);
			ptrs[idx] = NULL;
		} else {
			if (mallocLoud) {
				putsn(" realloc ");
				puthex(idx);
				putsn(" ");
				puthex(sz);
				putsn(" = ");
			}
			void * res = realloc(ptrs[idx], sz);
			if (mallocLoud) {
				puthex((int) res);
				putsn("\n");
			}
			if (res)
				ptrs[idx] = res;
		}
	}
	for (int i = 0; i < 0x40; i++) {
		free(ptrs[i]);
	}
}

void string_tests() {

	puts("performing strlen tests...");

	assert(strlen("") == 0);
	assert(strlen("a") == 1);
	assert(strlen("aa") == 2);
	assert(strlen("aaa") == 3);
	assert(strlen("aaaa") == 4);
	assert(strlen("aaaaa") == 5);


}

void time_tests() {

}

void ctype_tests() {

}

void setjmp_tests() {

}
