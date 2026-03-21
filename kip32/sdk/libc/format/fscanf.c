#include <stdarg.h>
#include <stdio.h>

int fscanf(FILE * restrict stream, const char * restrict format, ...) {
	va_list ap;
	va_start(ap, format);
	int res = vfscanf(stream, format, ap);
	va_end(ap);
	return res;
}
