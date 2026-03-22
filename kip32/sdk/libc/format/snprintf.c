#include <stdarg.h>
#include <stdio.h>

int snprintf(char * restrict s, size_t n, const char * restrict format, ...) {
	va_list ap;
	/* n - 1 ensures room for null terminator */
	struct _KIP32_LIBC_BUFFILE stream = __kip32_libc_buffile(s, 0, n - 1);
	va_start(ap, format);
	int res = vfprintf(&stream.base, format, ap);
	stream.buf[stream.pos] = 0;
	va_end(ap);
	return res;
}
