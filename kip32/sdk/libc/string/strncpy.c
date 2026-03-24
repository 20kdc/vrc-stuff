#include <string.h>

char * strncpy(char * restrict dest, const char * restrict src, size_t n) {
	size_t n2 = strlen(src) + 1;
	if (n2 > n)
		n2 = n;
	memcpy(dest, src, n2);
	if (n2 < n)
		memset(dest + n2, 0, n - n2);
	return dest;
}
