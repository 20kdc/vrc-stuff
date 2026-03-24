#include <string.h>

char * strchr(const char * s, int c) {
	char c2 = (char) c;
	while (1) {
		unsigned char c3 = *s;
		if (c2 == c3)
			return (char *) s;
		else if (!c3)
			break;
		s++;
	}
	return NULL;
}
