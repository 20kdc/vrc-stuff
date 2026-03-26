#include <stdio.h>
#include <ctype.h>

#define LENGTH_FLAGS_L1 1
#define LENGTH_FLAGS_L2 2
#define LENGTH_FLAGS_H1 4
#define LENGTH_FLAGS_H2 8
#define LENGTH_FLAGS_J 16
#define LENGTH_FLAGS_Z 32
#define LENGTH_FLAGS_T 64
#define LENGTH_FLAGS_LD 128
#define LENGTH_FLAGS_NOASSIGN 256

#define TGETC() \
{ \
	fch = fgetc(stream); \
	if (fch != EOF) \
		pos++; \
}
#define TUNGETC() \
{ \
	ungetc(fch, stream); \
	pos--; \
}

int vfscanf(FILE * restrict stream, const char * restrict format, va_list arg) {
	int result = EOF;
	size_t pos = 0;
	while (*format) {
		unsigned char chr = *format;
		int fch;
		if (chr == '%') {
			/* NYI */
			break;
		} else if (isspace(chr)) {
			/* whitespace */
			while (1) {
				TGETC();
				if (fch == EOF) {
					break;
				} else if (!isspace(fch)) {
					TUNGETC();
					break;
				}
			}
		} else {
			/* ordinary */
			TGETC();
			if (chr != (char) fch) {
				TUNGETC();
				break;
			} else {
				/* consumed successfully */
			}
		}
		format++;
	}
	return result;
}
