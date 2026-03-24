#include <string.h>

size_t strxfrm(char * restrict s1, const char * restrict s2, size_t n) {
	size_t tfs = strlen(s2);
	if (n)
		strncpy(s1, s2, n);
	return tfs;
}
