#include <string.h>

int strcmp(const char * a, const char * b) {
	while (1) {
		signed char ac = *a;
		signed char bc = *b;
		signed char diff = ac - bc;
		if (diff != 0)
			return diff;
		if (ac == 0)
			return 0;
		a++;
		b++;
	}
}

int strcoll(const char * a, const char * b) __attribute__((alias("strcmp")));
