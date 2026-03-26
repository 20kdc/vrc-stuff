#include <stdio.h>

int fgetc(FILE * stream) {
	if (stream->flags & __KIP32_LIBC_FILEFLAG_UNGETC) {
		int res = stream->flags & __KIP32_LIBC_FILEFLAG_UNGETC_CHAR;
		stream->flags &= ~(__KIP32_LIBC_FILEFLAG_UNGETC | __KIP32_LIBC_FILEFLAG_UNGETC_CHAR);
		return res;
	} else if (stream->flags & __KIP32_LIBC_FILEFLAG_EOF) {
		return EOF;
	}
	return stream->getc(stream);
}

int getc(FILE * stream) __attribute__((alias("fgetc")));
