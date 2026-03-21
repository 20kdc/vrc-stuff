#include <stdio.h>

size_t fread(void * __restrict__ ptr, size_t size, size_t nmemb, FILE * __restrict__ stream) {
	size_t total = nmemb * size;
	if (!stream->read || total == 0)
		return 0;
	if (stream->ungetc != EOF) {
		*((char *) ptr) = stream->ungetc;
		stream->ungetc = EOF;
		total--;
		if (!total)
			return size;
		ptr++;
	}
	return stream->read(ptr, total, stream) / size;
}
