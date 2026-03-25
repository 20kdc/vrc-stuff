/*
 * uitoa implementation
 */

#ifndef UITOA_TYPE
#include <stdlib.h>
/* The type (ofc) */
#define UITOA_TYPE unsigned int
/* Symbol of the function */
#define UITOA_SYM uitoa
#endif

char * UITOA_SYM(UITOA_TYPE value, char * str, int radix) {
	/* We constrain the radix to be higher than unary to constrain the buffer size (which affects stack use) to logarithmic quantities. */
	if (radix <= 1) {
		*str = 0;
		return str;
	}
	/* measure length */
	int digits = 0;
	UITOA_TYPE valueTest = value;
	while (1) {
		digits++;
		valueTest /= radix;
		if (!valueTest)
			break;
	}
	/* write */
	str[digits] = 0;
	while (digits) {
		unsigned char digitValue = (unsigned char) (value % radix);
		value /= radix;
		char chr = (digitValue >= 10) ? 'a' + (digitValue - 10) : '0' + digitValue;
		digits--;
		str[digits] = chr;
	}
	return str;
}
