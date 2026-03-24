#include <string.h>

int memcmp(const void * a, const void * b, size_t n) {
	while (n) {
		char ac = *(const char *) a;
		char bc = *(const char *) b;
		char diff = ac - bc;
		if (diff)
			return diff;
		a++;
		b++;
		n--;
	}
	return 0;
}
