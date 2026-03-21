/*
 * strtol/strtoll implementation
 */

#ifndef STRTOL_TYPE
#define STRTOL_TYPE long
#define STRTOL_SYM strtol
#endif

#include <ctype.h>
#include <limits.h>

STRTOL_TYPE STRTOL_SYM(const char * __restrict__ nptr, char ** __restrict__ endptr, int base) {
	/* set early so we don't have to save it */
	if (endptr)
		*endptr = (char *) nptr;
	/* Skip initial whitespace */
	while (isspace(*nptr))
		nptr++;
	/*
	 * Adjusts if things are subtraction-based.
	 * Keep in mind STRTOL_TYPE can be unsigned!
	 */
	int sub = 0;
	if (*nptr == '-') {
		sub = 1;
		nptr++;
	} else if (*nptr == '+') {
		/* ignored! */
		nptr++;
	}
	/* Base-specific setup */
	if (base == 0) {
		/* So the spec here is a bit all-over-the-place in how it says it, but all it really wants us to do here is parse a prefix. */
		if (nptr[0] == '0') {
			if (nptr[1] == 'x' || nptr[1] == 'X') {
				base = 16;
				nptr += 2;
			} else {
				/*
				 * Technically this means we will parse a single '0' in octal. Fun!
				 * (For this reason, we don't consume the '0'.)
				 */
				base = 8;
			}
		} else {
			base = 10;
		}
	} else if (base == 16) {
		/* Skip "0x"/"0X" */
		if (nptr[0] == '0' && (nptr[1] == 'x' || nptr[1] == 'X'))
			nptr += 2;
	}
	/* Parsing */
	int atLeastOneDigit = 0;
	STRTOL_TYPE result = 0;
	while (1) {
		char chr = *nptr;
		/*
		 * figure out the digit's value.
		 * UINT_MAX is used so the base number can't exceed it.
		 * unsigned was chosen over int because UINT_MAX encodes as -1, which is a trivial immediate
		 */
		unsigned val = UINT_MAX;
		if (chr >= '0' && chr <= '9')
			val = chr - '0';
		else if (chr >= 'a' && chr <= 'z')
			val = (chr - 'a') + 10;
		else if (chr >= 'A' && chr <= 'Z')
			val = (chr - 'A') + 10;
		if (val >= (unsigned) base)
			break;
		/* apply the digit */
		result = result * base;
		if (sub) {
			result -= val;
		} else {
			result += val;
		}
		/* another char done */
		atLeastOneDigit = 1;
		nptr++;
	}
	/* Cleanup */
	if (atLeastOneDigit && endptr)
		*endptr = (char *) nptr;
	return result;
}
