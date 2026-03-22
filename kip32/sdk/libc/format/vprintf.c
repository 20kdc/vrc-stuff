#include <stdarg.h>
#include <stdio.h>

int vprintf(const char * restrict format, va_list arg) {
	return vfprintf(stdout, format, arg);
}
