#include <stdio.h>

int ungetc(int c, FILE * stream) {
	if (c == EOF)
		return EOF;
	if (stream->flags & __KIP32_LIBC_FILEFLAG_UNGETC)
		return EOF;
	c &= __KIP32_LIBC_FILEFLAG_UNGETC_CHAR;
	stream->flags &= ~(__KIP32_LIBC_FILEFLAG_EOF | __KIP32_LIBC_FILEFLAG_UNGETC_CHAR);
	stream->flags |= c | __KIP32_LIBC_FILEFLAG_UNGETC;
	return c;
}
