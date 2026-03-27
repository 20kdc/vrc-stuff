#include <string.h>

void * memset(void * s, int c, size_t n) {
	/*
	 * wow my first pass on this was BROKEN huh
	 * kinda forgot the 'c' parameter
	 * bootstrap up to 4 chars
	 */
	size_t already = 0;
	if (n > 3) {
		((char *) s)[0] = c;
		((char *) s)[1] = c;
		((char *) s)[2] = c;
		((char *) s)[3] = c;
		already = 4;
	} else if (n > 2) {
		((char *) s)[0] = c;
		((char *) s)[1] = c;
		((char *) s)[2] = c;
		already = 3;
	} else if (n > 1) {
		((char *) s)[0] = c;
		((char *) s)[1] = c;
		already = 2;
	} else if (n > 0) {
		((char *) s)[0] = c;
		already = 1;
	} else {
		return s;
	}
	/* fill-in. this will naturally exponentially double and then likely fill the rest in quickly */
	while (already < n) {
		size_t amount = n - already;
		if (amount > already)
			amount = already;
		memcpy(s + already, s, amount);
		already += amount;
	}
	return s;
}
