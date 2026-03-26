#include <stdio.h>
#include <ctype.h>

#include "lengthflags.h"

#define CONV_FLAGS_NOASSIGN 1

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
		unsigned char chr = *(format++);
		int fch;
		if (chr == '%') {
			int lengthFlags = 0;
			int convFlags = 0;
			while (1) {
				chr = *(format++);
				if (chr == '%') {
					goto ordinary;
				} else if (chr == '*') {
					convFlags |= CONV_FLAGS_NOASSIGN;
				} else if (__kip32_libc_lengthflags_tryconsume(&lengthFlags, chr)) {
					/* lhjztL */
				} else {
					/* unknown! */
					return result;
				}
			}
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
			ordinary:
			/* ordinary */
			TGETC();
			if (chr != (char) fch) {
				TUNGETC();
				break;
			} else {
				/* consumed successfully */
			}
		}
	}
	return result;
}
