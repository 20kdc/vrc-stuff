#include <stdarg.h>
#include <stdio.h>

int scanf(const char * restrict format, ...) {
	va_list ap;
	va_start(ap, format);
	int res = vfscanf(stdin, format, ap);
	va_end(ap);
	return res;
}
