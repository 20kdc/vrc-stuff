#include <string.h>

void * memmove(void * dest, const void * src, size_t n) {
	const char * srcc = src;
	char * dstc = dest;
	/*
	 * we mustn't hit our own writing pointer
	 * if dest > src, then must go in reverse, because:
	 *  v
	 *  ABCDE
	 *   ^
	 *   v
	 *  AACDE
	 *    ^
	 */
	int reverse = dest > src;
	if (reverse) {
		srcc += n;
		dstc += n;
	}
	while (n) {
		if (reverse) {
			dstc--;
			srcc--;
		}
		*dstc = *srcc;
		if (!reverse) {
			dstc++;
			srcc++;
		}
		n--;
	}
	return dest;
}

void * memcpy(void * restrict dest, const void * restrict src, size_t n) __attribute__((alias("memmove")));
