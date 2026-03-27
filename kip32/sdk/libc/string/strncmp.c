#include <string.h>

int strncmp(const char * a, const char * b, size_t n) {
	while (n > 0) {
		signed char ac = *a;
		signed char bc = *b;
		signed char diff = ac - bc;
		if (diff != 0)
			return diff;
		if (ac == 0)
			return 0;
		a++;
		b++;
		n--;
	}
	return 0;
}
