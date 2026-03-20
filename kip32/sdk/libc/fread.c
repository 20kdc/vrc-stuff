#include <stdio.h>

size_t fread(void * __restrict__ ptr, size_t size, size_t nmemb, FILE * __restrict__ stream) {
	if (size == 0)
		return 0;
	return stream->read(ptr, nmemb * size, stream) / size;
}
