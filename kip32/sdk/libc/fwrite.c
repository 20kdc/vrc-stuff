#include <stdio.h>

size_t fwrite(const void * __restrict__ ptr, size_t size, size_t nmemb, FILE * __restrict__ stream) {
	if (size == 0)
		return 0;
	return stream->write(ptr, nmemb * size, stream) / size;
}
