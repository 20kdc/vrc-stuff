#include <string.h>

int memcmp(const void * a, const void * b, size_t n) {
	const void * ae = a + n;
	while (a != ae) {
		signed char ac = *(const signed char *) a;
		signed char bc = *(const signed char *) b;
		signed char diff = ac - bc;
		if (diff)
			return diff;
		a++;
		b++;
	}
	return 0;
}
