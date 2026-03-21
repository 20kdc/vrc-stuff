#include <stdio.h>

int ferror(FILE * stream) {
	return stream->flags & __KIP32_LIBC_FILEFLAG_ERROR;
}
