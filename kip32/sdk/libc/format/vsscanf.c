#include <stdio.h>
#include <string.h>

int vsscanf(const char * restrict s, const char * restrict format, va_list arg) {
	struct _KIP32_LIBC_BUFFILE stream = __kip32_libc_buffile((char *) s, 0, strlen(s));
	return vfscanf(&stream.base, format, arg);
}
