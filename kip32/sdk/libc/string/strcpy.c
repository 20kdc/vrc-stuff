#include <string.h>

char * strcpy(char * restrict dest, const char * restrict src) {
	return memcpy(dest, src, strlen(src) + 1);
}
