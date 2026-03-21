#include <stdio.h>

size_t fread(void * __restrict__ ptr, size_t size, size_t nmemb, FILE * __restrict__ stream) {
	size_t total = nmemb * size;
	if (!total)
		return 0;
	if (stream->flags & __KIP32_LIBC_FILEFLAG_UNGETC) {
		*((char *) ptr) = stream->flags & __KIP32_LIBC_FILEFLAG_UNGETC_CHAR;
		stream->flags &= ~__KIP32_LIBC_FILEFLAG_UNGETC;
		total--;
		if (!total)
			return size;
		ptr++;
	} else if (stream->flags & __KIP32_LIBC_FILEFLAG_EOF) {
		return 0;
	}
	return stream->read(ptr, total, stream) / size;
}
