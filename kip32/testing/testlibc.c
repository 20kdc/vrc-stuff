#include "testlibc.h"

int system(const char * text) {
	while (*text) {
		putchar(*text);
		text++;
	}
	return 0;
}

int puts(const char * text) {
	system(text);
	putchar(10);
	return 1;
}

void puthex(int v) {
	putchar('0');
	putchar('x');
	for (int i = 0; i < 8; i++) {
		putchar(("0123456789ABCDEF")[(v >> 28) & 0xF]);
		v <<= 4;
	}
}

