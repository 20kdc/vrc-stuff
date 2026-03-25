/*
 * itoa implementation
 */

#ifndef ITOA_TYPE
#include <stdlib.h>
/* The type (ofc) */
#define ITOA_TYPE int
/* Symbol of the function */
#define ITOA_SYM itoa
/* Provide the unsigned symbol here. */
#define ITOA_UNSIGNED_SYM uitoa
/* Provide the unsigned type here. */
#define ITOA_UNSIGNED_TYPE unsigned int
#endif

char * ITOA_SYM(ITOA_TYPE value, char * str, int radix) {
	if (value < 0) {
		*str = '-';
		ITOA_UNSIGNED_TYPE mod = (ITOA_UNSIGNED_TYPE) value;
		mod = 0 - mod;
		ITOA_UNSIGNED_SYM(mod, str + 1, radix);
		return str;
	} else {
		return ITOA_UNSIGNED_SYM((ITOA_UNSIGNED_TYPE) value, str, radix);
	}
}
