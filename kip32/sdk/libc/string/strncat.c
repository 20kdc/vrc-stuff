#include <string.h>

char * strncat(char * restrict dest, const char * restrict src, size_t n) {
	char * destSave = dest;
	dest += strlen(dest);
	/* this function is weird ...*/
	while (n) {
		char sc = *src;
		if (!sc)
			break;
		*dest = sc;
		dest++;
		src++;
		n--;
	}
	*dest = 0;
	return destSave;
}
