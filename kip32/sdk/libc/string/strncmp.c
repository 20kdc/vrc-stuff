#include <string.h>

int strncmp(const char * a, const char * b, size_t n) {
	while (n > 0) {
		char ac = *a;
		char bc = *b;
		char diff = ac - bc;
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
