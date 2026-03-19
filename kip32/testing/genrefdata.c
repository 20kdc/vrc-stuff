#include "muldiv.h"
#include "qemu.h"

/*
 * This program is meant to run in QEMU.
 * It is designed to probe QEMU's interpretation of operations that are 'prone to error', namely multiplication and division.
 */

int strlen(const char * str) {
	int res = 0;
	while (*str) {
		str++;
		res++;
	}
	return res;
}

void putchar(char chr) {
	write(1, &chr, 1);
}

void putsn(const char * text) {
	write(1, text, strlen(text));
}

void puts(const char * text) {
	putsn(text);
	putchar(10);
}

void puthex(int v) {
	putchar('0');
	putchar('x');
	for (int i = 0; i < 8; i++) {
		putchar(("0123456789ABCDEF")[(v >> 28) & 0xF]);
		v <<= 4;
	}
}

void write_case(const char * case_type, int (*process)(int, int), int v1, int v2) {
	putchar('\t');
	putsn(case_type);
	putsn(", ");
	puthex(v1);
	putsn(", ");
	puthex(v2);
	putsn(", ");
	puthex(process(v1, v2));
	puts(",");
}

void _start() {
	puts("/* generated with genrefdata.c */");
	puts("#define GRDC_END 0");
	puts("#define GRDC_MUL 1");
	puts("#define GRDC_MULH 2");
	puts("#define GRDC_MULHSU 3");
	puts("#define GRDC_MULHU 4");
	puts("#define GRDC_DIV 5");
	puts("#define GRDC_DIVU 6");
	puts("#define GRDC_REM 7");
	puts("#define GRDC_REMU 8");
	puts("int genrefdata_cases[] = {");
	for (int i = 0; i < 0x100; i++) {
		// The idea behind this arrangement is that bits 'in the middle' don't really differ in interpretation.
		// The only bits that matter are:
		// 1. The two top bits (arguably, one, but two for safety)
		// 2. The two bottom bits (arguably, one, but two for safety)
		int a = i & 0x3;
		int b = (i & 0xC) >> 2;
		int c = (i & 0x30) >> 4;
		int d = (i & 0xC0) >> 6;
		int v1 = (a << 30) | b;
		int v2 = (c << 30) | d;
		write_case("GRDC_MUL   ", muldiv_mul, v1, v2);
		write_case("GRDC_MULH  ", muldiv_mulh, v1, v2);
		write_case("GRDC_MULHSU", muldiv_mulhsu, v1, v2);
		write_case("GRDC_MULHU ", muldiv_mulhu, v1, v2);
		write_case("GRDC_DIV   ", muldiv_div, v1, v2);
		write_case("GRDC_DIVU  ", muldiv_divu, v1, v2);
		write_case("GRDC_REM   ", muldiv_rem, v1, v2);
		write_case("GRDC_REMU  ", muldiv_remu, v1, v2);
	}
	puts("\tGRDC_END");
	puts("};");
	_exit(0);
}
