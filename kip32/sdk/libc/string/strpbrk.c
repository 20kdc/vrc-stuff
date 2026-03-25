#include <string.h>
#include "strpbrk_tbl.h"

char * strpbrk(const char * haystack, const char * needles) {
	/* initialize acceleration table */
	memset(strpbrk_flags, 0, 256);
	while (*needles) {
		strpbrk_flags[(unsigned char) *needles] = 1;
		needles++;
	}
	/* find target */
	while (*haystack) {
		if (strpbrk_flags[(unsigned char) *haystack])
			return (char *) haystack;
		haystack++;
	}
	return NULL;
}
