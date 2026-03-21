#include <stdio.h>

void clearerr(FILE * stream) {
	stream->flags &= ~(__KIP32_LIBC_FILEFLAG_EOF | __KIP32_LIBC_FILEFLAG_ERROR);
}
