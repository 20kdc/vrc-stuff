#include <string.h>

char * strstr(const char * haystack, const char * needle) {
	if (!*needle)
		return (char *) haystack;
	while (*haystack) {
		if (!strcmp(haystack, needle))
			return (char *) haystack;
		haystack++;
	}
	return NULL;
}
