#include <string.h>

void * memchr(const void * s, int c, size_t n) {
	unsigned char c2 = (unsigned char) c;
	const void * end = s + n;
	while (s != end) {
		unsigned char c3 = *((const unsigned char *) s);
		if (c2 == c3)
			return (void *) s;
		s++;
	}
	return NULL;
}
