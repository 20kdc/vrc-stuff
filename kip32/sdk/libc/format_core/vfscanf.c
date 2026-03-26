#include <limits.h>
#include <stdio.h>
#include <stdint.h>
#include <string.h>

#include "lengthflags.h"
#include "../string/strpbrk_tbl.h"
#include "../ctype_i.h"

#define TGETC() \
{ \
	fch = fgetc(stream); \
	if (fch != EOF) \
		pos++; \
}

/* TGETC with field start/width check. */
#define TFGETC() \
{ \
	fch = EOF; \
	if ((pos - fieldStart) < fieldWidth) \
		TGETC(); \
}

/* ungetc with an EOF check and position adjustment */
#define TUNGETC() \
if (fch >= 0) { \
	ungetc(fch, stream); \
	pos--; \
}

static void skipWS(FILE * stream, size_t * pos) {
	/* whitespace */
	while (1) {
		int fch = fgetc(stream);
		if (fch < 0) {
			break;
		} else if (!ISSPACE(fch)) {
			ungetc(fch, stream);
			break;
		} else {
			(*pos)++;
		}
	}
}

int vfscanf(FILE * restrict stream, const char * restrict format, va_list arg) {
	int result = EOF;
	size_t pos = 0;
	while (*format) {
		unsigned char chr = *(format++);
		int fch;
		if (chr == '%') {
			int lengthFlags = 0;
			int noAssign = 0;
			int fieldNoTerminator = 0;
			char * fieldPtr = NULL;
			size_t fieldWidth = SIZE_MAX;
			/* The field must be exactly fieldWidth in size. */
			int fieldExact = 0;
			/* The field must be non-empty. */
			int fieldNonEmpty = 0;
			/* unsigned on purpose due to later logic */
			unsigned intConvRadix = 0;
			while (1) {
				chr = *(format++);
				if (chr == '*') {
					noAssign = 1;
				} else if (__kip32_libc_lengthflags_tryconsume(&lengthFlags, chr)) {
					/* lhjztL */
				} else if (chr >= '0' && chr <= '9') {
					if (fieldWidth == SIZE_MAX) {
						fieldWidth = 0;
					} else {
						fieldWidth *= 10;
					}
					fieldWidth += chr - '0';
				} else if (chr == 'd') {
					intConvRadix = 10;
					goto intConvEngine;
				} else if (chr == 'i' || chr == 'p') {
					intConvRadix = 0;
					goto intConvEngine;
				} else if (chr == 'o') {
					intConvRadix = 8;
					goto intConvEngine;
				} else if (chr == 'u') {
					intConvRadix = 10;
					goto intConvEngine;
				} else if (chr == 'x') {
					intConvRadix = 16;
					goto intConvEngine;
				} else if (chr == 'c') {
					fieldNoTerminator = 1;
					fieldExact = 1;
					if (fieldWidth == SIZE_MAX)
						fieldWidth = 1;
					if (!noAssign)
						fieldPtr = va_arg(arg, char *);
					memset(__kip32_libc_strpbrk_flags, 1, 256);
					goto fieldEngine;
				} else if (chr == 's') {
					skipWS(stream, &pos);
					if (!noAssign)
						fieldPtr = va_arg(arg, char *);
					for (int i = 0; i < 256; i++)
						__kip32_libc_strpbrk_flags[i] = !ISSPACE(i);
					goto fieldEngine;
				} else if (chr == '[') {
					/* do setup */
					skipWS(stream, &pos);
					if (!noAssign)
						fieldPtr = va_arg(arg, char *);
					fieldNonEmpty = 1;
					/* parse the set... */
					char setVal = 1;
					/* this check */
					if ((*format) == '^') {
						format++;
						setVal = 0;
						memset(__kip32_libc_strpbrk_flags, 1, 256);
					} else {
						memset(__kip32_libc_strpbrk_flags, 0, 256);
					}
					/* [] check */
					if ((*format) == ']') {
						__kip32_libc_strpbrk_flags[']'] = setVal;
						format++;
					}
					/* setup the set */
					while (1) {
						chr = *(format++);
						if (chr == 0) {
							/* ran off of end */
							return result;
						} else if (chr == ']') {
							break;
						} else {
							__kip32_libc_strpbrk_flags[chr] = setVal;
						}
					}
					goto fieldEngine;
				} else if (chr == 'n') {
					void * v = va_arg(arg, void *);
					__kip32_libc_lengthflags_write(lengthFlags, v, pos);
					break;
				} else if (chr == '%') {
					/*
					 * Step 8 still applies. Need to check if this is actually 'supposed' to happen.
					 * skipWS(stream, &pos);
					 */
					goto ordinary;
				} else {
					/* unknown! */
					return result;
				}
			}
			continue;

			/*
			 * The 'field engine' handles 's', 'c' and '[' requests.
			 * Specifically, it reads a sequence of characters, filtered by __kip32_libc_strpbrk_flags.
			 * Unless fieldNoTerminator is set, a null terminator is added.
			 */
			fieldEngine:
			{
				size_t fieldIdx = 0;
				while (fieldIdx < fieldWidth) {
					TGETC();
					if (fch < 0) {
						break;
					} else if (!__kip32_libc_strpbrk_flags[fch & 0xFF]) {
						TUNGETC();
						break;
					}
					if (fieldPtr)
						fieldPtr[fieldIdx] = fch;
					fieldIdx++;
				}
				if (fieldExact && (fieldIdx != fieldWidth))
					return result;
				if (fieldIdx == 0 && fieldNonEmpty)
					return result;
				if (fieldPtr && !fieldNoTerminator)
					fieldPtr[fieldIdx] = 0;
				if (fieldPtr)
					goto assignSuccess;
				continue;
			}

			intConvEngine:
			{
				skipWS(stream, &pos);
				size_t fieldStart = pos;

				/* maybeSign */
				TFGETC();
				int maybeSign = fch;
				if (maybeSign != '-' && maybeSign != '+')
					TUNGETC();

				/*
				 * This needs to be settable early, because we can't backtrack if we read a single 0 in radix 0 or 16.
				 * Basically, both of these modes may try to consume the prefix "0x", and in the process be unable to recover the "0".
				 * We can't ungetc twice. Luckily, the 0 digit has no effect on the result outside of possibly changing base.
				 * So if we encounter a lone zero digit like this, setting atLeastOneDigit simulates the effect it would have had.
				 */
				int atLeastOneDigit = 0;

				/* radix discovery etc. */
				if (intConvRadix == 0) {
					TFGETC();
					if (fch == '0') {
						atLeastOneDigit = 1;
						TFGETC();
						if (fch == 'x' || fch == 'X') {
							intConvRadix = 16;
						} else {
							TUNGETC();
							intConvRadix = 8;
						}
					} else {
						TUNGETC();
						intConvRadix = 10;
					}
				} else if (intConvRadix == 16) {
					TFGETC();
					if (fch == '0') {
						atLeastOneDigit = 1;
						TFGETC();
						if (!(fch == 'x' || fch == 'X'))
							TUNGETC();
					} else {
						TUNGETC();
					}
				}

				/* main body */
				int sub = maybeSign == '-';
				long long result = 0;
				while (1) {
					TFGETC();
					if (fch < 0)
						break;
					/*
					 * figure out the digit's value.
					 * UINT_MAX is used so the base number can't exceed it.
					 * unsigned was chosen over int because UINT_MAX encodes as -1, which is a trivial immediate
					 */
					unsigned val = UINT_MAX;
					if (fch >= '0' && fch <= '9')
						val = fch - '0';
					else if (fch >= 'a' && fch <= 'z')
						val = (fch - 'a') + 10;
					else if (fch >= 'A' && fch <= 'Z')
						val = (fch - 'A') + 10;
					if (val >= intConvRadix) {
						TUNGETC();
						break;
					}
					/* apply the digit */
					result = result * (long long) intConvRadix;
					if (sub) {
						result -= val;
					} else {
						result += val;
					}
					/* another char done */
					atLeastOneDigit = 1;
				}
				/* Cleanup */
				if (atLeastOneDigit) {
					if (!noAssign) {
						void * fld = va_arg(arg, void *);
						__kip32_libc_lengthflags_write(lengthFlags, fld, result);
						goto assignSuccess;
					} else {
						continue;
					}
				} else {
					/* failed! */
					return result;
				}
			}

			/*
			 * Any successful assigning conversion goes here to adjust the result and leave.
			 */
			assignSuccess:
			if (result < 0) {
				result = 1;
			} else {
				result++;
			}
		} else if (ISSPACE(chr)) {
			skipWS(stream, &pos);
		} else {
			ordinary:
			/* ordinary */
			TGETC();
			if (chr != (char) fch) {
				TUNGETC();
				break;
			} else {
				/* consumed successfully */
			}
		}
	}
	return result;
}
