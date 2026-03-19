#include "testlibc.h"

int strlen(const char * str) {
	int res = 0;
	while (*str) {
		str++;
		res++;
	}
	return res;
}

void putsn(const char * text) {
	while (*text) {
		putchar(*text);
		text++;
	}
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

