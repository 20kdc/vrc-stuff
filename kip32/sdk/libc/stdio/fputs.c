#include <stdio.h>
#include <string.h>

int fputs(const char * restrict s, FILE * restrict stream) {
	size_t len = strlen(s);
	if (!len)
		return 1;
	if (stream->write(s, len, stream) < len)
		return EOF;
	return 1;
}
