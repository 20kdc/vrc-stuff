#include <stdio.h>

int fputc(int c, FILE * stream) {
	return stream->putc(c, stream);
}

int putc(int c, FILE * stream) __attribute__((alias("fputc")));
