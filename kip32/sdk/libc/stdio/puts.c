#include <stdio.h>
#include <string.h>

int puts(const char * s) {
	size_t len = strlen(s);
	if (len)
		if (stdout->write(s, len, stdout) < len)
			return EOF;
	if (stdout->putc(10, stdout) == EOF)
		return EOF;
	return 1;
}
