#include <stdarg.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <inttypes.h>

#define LENGTH_FLAGS_L1 1
#define LENGTH_FLAGS_L2 2
#define LENGTH_FLAGS_H1 4
#define LENGTH_FLAGS_H2 8
#define LENGTH_FLAGS_J 16
#define LENGTH_FLAGS_Z 32
#define LENGTH_FLAGS_T 64
#define LENGTH_FLAGS_LD 128
#define LENGTH_FLAGS_ALIGN_LEFT 256
#define LENGTH_FLAGS_FORCE_SIGN 512
#define LENGTH_FLAGS_SPACE 1024
#define LENGTH_FLAGS_ALT 2048
#define LENGTH_FLAGS_PRECISION 4096
#define LENGTH_FLAGS_ZERO 8192

#define TPUTC(v) { \
	if (fputc(v, stream) < 0) \
		return -1; \
	total++; \
}

#define TPUTB(buf, len) { \
	if (fwrite(buf, 1, len, stream) != len) \
		return -1; \
	total += len; \
}

int vfprintf(FILE * restrict stream, const char * restrict format, va_list arg) {
	int total = 0;
	while (*format) {
		const char * q = (const char *) strchr(format, '%');
		size_t litLen = 0;
		if (q) {
			litLen = q - format;
		} else {
			litLen = strlen(format);
		}
		if (litLen)
			TPUTB(format, litLen);
		/* if we didn't actually hit a %, we're out of string now for sure */
		if (!q)
			break;
		/* we know the % says % */
		format = q + 1;
		int lengthFlags = 0;
		int precision = -1;
		int fieldWidth = -1;
		int inInteger = 0;
		while (1) {
			char chr = *(format++);
			if (!chr)
				return -1;
			/* technically, it's UB to have an invalid conversion spec, so we can be reasonably 'permissive' here */
			/*
			 * setting this queues an integer conversion
			 * which terminates the format
			 * negative = unsigned
			 */
			int intConvRadix = 0;
			int intConvUpper = 0;
			/*
			 * Conversion buffer for 'immediate' values
			 * Not necessarily zero-terminated!
			 */
			char cvBuf[__KIP32_LIBC_LLTOA_BUFSIZE];
			/*
			 * setting this begins output stage
			 * which terminates the format
			 */
			const char * cvPtr = 0;
			size_t cvLen = 0;
			/* regular ol' parsing flag */
			int inIntegerNext = 0;
			if (chr == '%') {
				cvBuf[0] = chr;
				cvPtr = cvBuf;
				cvLen = 1;
			} else if (chr == 'c') {
				chr = va_arg(arg, int);
				cvBuf[0] = chr;
				cvPtr = cvBuf;
				cvLen = 1;
			} else if (chr == 's') {
				const char * str = va_arg(arg, const char *);
				size_t lenToWrite;
				if (precision >= 0) {
					lenToWrite = 0;
					while (lenToWrite < precision) {
						if (!str[lenToWrite])
							break;
						lenToWrite++;
					}
				} else {
					lenToWrite = strlen(str);
				}
				cvPtr = str;
				cvLen = lenToWrite;
			} else if (chr == 'p') {
				lengthFlags = LENGTH_FLAGS_L1;
				intConvRadix = 16;
				intConvUpper = 1;
				precision = 8;
			} else if (chr == 'n') {
				void * ifo = va_arg(arg, void *);
				if (lengthFlags & LENGTH_FLAGS_H2) {
					*((char *) ifo) = (char) total;
				} else if (lengthFlags & LENGTH_FLAGS_H1) {
					*((short *) ifo) = (short) total;
				} else if (lengthFlags & LENGTH_FLAGS_L2) {
					*((long long *) ifo) = (long long) total;
				} else if (lengthFlags & LENGTH_FLAGS_J) {
					*((intmax_t *) ifo) = (intmax_t) total;
 				} else {
					/* LD makes no sense here. note L1/Z/T are ignored b/c they resolve to int-ish anyway */
					*((int *) ifo) = total;
				}
				break;
			} else if (chr == 'i' || chr == 'd') {
				intConvRadix = 10;
			} else if (chr == 'o') {
				intConvRadix = -8;
			} else if (chr == 'u') {
				intConvRadix = -10;
			} else if (chr == 'x') {
				intConvRadix = -16;
			} else if (chr == 'X') {
				intConvRadix = -16;
				intConvUpper = 1;
			} else if (chr == '-') { /* flags */
				lengthFlags |= LENGTH_FLAGS_ALIGN_LEFT;
			} else if (chr == '+') {
				lengthFlags |= LENGTH_FLAGS_FORCE_SIGN;
			} else if (chr == ' ') {
				lengthFlags |= LENGTH_FLAGS_SPACE;
			} else if (chr == '#') {
				lengthFlags |= LENGTH_FLAGS_ALT;
			} else if (chr == 'l') { /* length modifiers */
				lengthFlags |= (lengthFlags & LENGTH_FLAGS_L1) ? LENGTH_FLAGS_L2 : LENGTH_FLAGS_L1;
			} else if (chr == 'h') {
				lengthFlags |= (lengthFlags & LENGTH_FLAGS_H1) ? LENGTH_FLAGS_H2 : LENGTH_FLAGS_H1;
			} else if (chr == 'j') {
				lengthFlags |= LENGTH_FLAGS_J;
			} else if (chr == 'z') {
				lengthFlags |= LENGTH_FLAGS_Z;
			} else if (chr == 't') {
				lengthFlags |= LENGTH_FLAGS_T;
			} else if (chr == 'L') {
				lengthFlags |= LENGTH_FLAGS_LD;
			} else if (chr == '.') { /* precision */
				lengthFlags |= LENGTH_FLAGS_PRECISION;
				inIntegerNext = 1;
				precision = 0;
			} else if (chr == '*') { /* asterisk */
				int val = va_arg(arg, int);
				if (lengthFlags & LENGTH_FLAGS_PRECISION) {
					precision = val;
				} else {
					if (val < 0) {
						fieldWidth = -val;
						lengthFlags |= LENGTH_FLAGS_ALIGN_LEFT;
					} else {
						fieldWidth = val;
					}
				}
			} else if (chr >= '0' && chr <= '9') { /* decimal digits (precision etc.) */
				int * theField = (lengthFlags & LENGTH_FLAGS_PRECISION) ? &precision : &fieldWidth;
				if (inInteger || (chr != '0')) {
					if (*theField == -1) {
						*theField = 0;
					} else {
						*theField *= 10;
					}
					*theField += chr - '0';
					inIntegerNext = 1;
				} else {
					lengthFlags |= LENGTH_FLAGS_ZERO;
				}
			} else {
				/* unknown! */
				return -1;
			}
			inInteger = inIntegerNext;
			/*
			 * used for the 'sign and base' section of cvPtr
			 * this only gets set in the integer converter
			 */
			char signAndBase[4] = {};
			size_t sabLen = 0;
			/* Integer Conversion */
			if (intConvRadix) {
				/* "+" + "0x" + lltoa */
				long long val;
				int llMode = 0;
				if (lengthFlags & LENGTH_FLAGS_H2) {
					val = va_arg(arg, int);
					val = intConvRadix < 0 ? ((unsigned char) val) : ((signed char) val);
				} else if (lengthFlags & LENGTH_FLAGS_H1) {
					val = va_arg(arg, int);
					val = intConvRadix < 0 ? ((unsigned short) val) : ((signed short) val);
				} else if (lengthFlags & (LENGTH_FLAGS_L2 | LENGTH_FLAGS_J)) {
					val = va_arg(arg, long long);
					llMode = 1;
				} else {
					/* LD makes no sense here. note L1/Z/T are ignored b/c they resolve to int-ish anyway */
					val = va_arg(arg, int);
				}
				/* implement precision rules */
				if (precision < 0)
					precision = 1;
				/* primary conversion */
				if (llMode) {
					if (intConvRadix < 0) {
						ulltoa(val, cvBuf, -intConvRadix);
					} else {
						lltoa(val, cvBuf, intConvRadix);
					}
				} else {
					if (intConvRadix < 0) {
						uitoa(val, cvBuf, -intConvRadix);
					} else {
						itoa(val, cvBuf, intConvRadix);
					}
				}
				cvPtr = cvBuf;
				/*
				 * we delete the sign character here because of various prefixing/etc.
				 * we'll put it back later via other means if needed
				 */
				if (cvPtr[0] == '-') {
					signAndBase[sabLen++] = '-';
					/* and gone */
					cvPtr++;
				} else if (lengthFlags & LENGTH_FLAGS_FORCE_SIGN) {
					signAndBase[sabLen++] = '+';
				}
				/*
				 * so here's the hard part: navigating the mess of flags to put this all together
				 * it's made up of these components:
				 * 1. space padding (left)
				 * 2. sign + base indication (signAndBase[sabLen])
				 * 4. zero padding (precision OR 0 flag)
				 * 5. content (bufPtr[wLen])
				 * 6. space padding (right)
				 */
				cvLen = strlen(cvPtr);
				/* Alt. flag handling. Uses the 'carved out' space at the start of the buffer. */
				if (lengthFlags & LENGTH_FLAGS_ALT) {
					if (intConvRadix == -16) {
						if (val != 0) {
							signAndBase[sabLen++] = '0';
							signAndBase[sabLen++] = intConvUpper ? 'X' : 'x';
						}
					} else if (intConvRadix == -8) {
						int fixedPrecision = (int) (cvLen + 1);
						if (fixedPrecision > precision)
							precision = fixedPrecision;
					}
				}
				/* v=0 p=0 elision, has to be implemented here since the alt flag can 'escape' deletion via precision */
				if (val == 0 && precision == 0)
					cvLen = 0;
				/* uppercase flag */
				if (intConvUpper) {
					/* because cvPtr is usually const, but we trust it here */
					char * cvm = (char *) cvPtr;
					for (size_t i = 0; i < cvLen; i++)
						cvm[i] = (cvm[i] >= 'a' && cvm[i] <= 'z') ? ((cvm[i] - 'a') + 'A') : cvm[i];
				}
			}
			/*
			 * -- This line separates 'things which are done during the int conversion' from 'things which are done in all fields'. --
			 */
			if (cvPtr) {
				/* 0 converts field width into precision, but only if this is an int conversion */
				if (intConvRadix && (lengthFlags & LENGTH_FLAGS_ZERO)) {
					/* since additional zeroes are generated by precision, this is a safe bet */
					int expectedPrecision = fieldWidth - sabLen;
					if (precision < expectedPrecision)
						precision = expectedPrecision;
					fieldWidth = 0;
				}
				/* calc precision zero count */
				int zeroCount = precision - cvLen;
				if (zeroCount < 0)
					zeroCount = 0;
				int leftSpaces = 0;
				int rightSpaces = 0;
				/* fieldWidth layout or straight-line layout; who will win? */
				if (fieldWidth > 0) {
					int w = sabLen + zeroCount + cvLen;
					if (w < fieldWidth) {
						if (lengthFlags & LENGTH_FLAGS_ALIGN_LEFT)
							rightSpaces = fieldWidth - w;
						else
							leftSpaces = fieldWidth - w;
					}
				}
				/* actually write */
				for (int i = 0; i < leftSpaces; i++)
					TPUTC(' ');
				TPUTB(signAndBase, sabLen);
				for (int i = 0; i < zeroCount; i++)
					TPUTC('0');
				TPUTB(cvPtr, cvLen);
				for (int i = 0; i < rightSpaces; i++)
					TPUTC(' ');
				break;
			}
		}
		/* having finally completed executing the format specifier, we move onto better things. like executing more of them */
	}
	return total;
}
