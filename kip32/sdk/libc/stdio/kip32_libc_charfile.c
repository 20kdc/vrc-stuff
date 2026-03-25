#include <stdio.h>

size_t __kip32_libc_charfile_read(void * restrict ptr, size_t size, FILE * restrict stream) {
	size_t pos = 0;
	while (pos != size) {
		int ch = stream->getc(stream);
		/* eof/error has already been marked*/
		if (ch < 0)
			break;
		((char *) ptr)[pos++] = ch;
	}
	return pos;
}

size_t __kip32_libc_charfile_write(const void * restrict ptr, size_t size, FILE * restrict stream) {
	size_t pos = 0;
	while (pos != size) {
		if (stream->putc(((char *) ptr)[pos], stream) < 0)
			break;
		pos++;
	}
	return pos;
}
