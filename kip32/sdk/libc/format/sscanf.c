#include <stdio.h>
#include <string.h>

int sscanf(const char * restrict s, const char * restrict format, ...) {
	va_list ap;
	struct _KIP32_LIBC_BUFFILE stream = __kip32_libc_buffile((char *) s, 0, strlen(s));
	va_start(ap, format);
	int res = vfscanf(&stream.base, format, ap);
	va_end(ap);
	return res;
}
