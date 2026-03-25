#include <string.h>
#include "strpbrk_tbl.h"

#ifndef STRSPN_SYM
#define STRSPN_SYM strspn
/*
 * only difference between strspn and strcspn is this
 * v is 1 if the character is in needles
 */
#define STRSPN_SHOULD_BREAK(in_needles) !(in_needles)
#endif

size_t STRSPN_SYM(const char * haystack, const char * needles) {
	/* initialize acceleration table */
	memset(strpbrk_flags, 0, 256);
	while (*needles) {
		strpbrk_flags[(unsigned char) *needles] = 1;
		needles++;
	}
	size_t segment = 0;
	/* find target */
	while (haystack[segment]) {
		if (STRSPN_SHOULD_BREAK(strpbrk_flags[(unsigned char) haystack[segment]]))
			break;
		segment++;
	}
	return segment;
}
