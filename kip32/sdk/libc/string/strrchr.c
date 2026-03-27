#include <string.h>

char * strrchr(const char * s, int c) {
	char c2 = (char) c;
    char * res = NULL;
	while (1) {
		char c3 = *s;
		if (c2 == c3)
			res = (char *) s;
		if (!c3)
			break;
		s++;
	}
	return res;
}
