#include <stdint.h>
#include <stdio.h>

int vsprintf(char * restrict s, const char * restrict format, va_list arg) {
	/* n - 1 ensures room for null terminator */
	struct _KIP32_LIBC_BUFFILE stream = __kip32_libc_buffile(s, 0, SIZE_MAX);
	int res = vfprintf(&stream.base, format, arg);
	stream.buf[stream.pos] = 0;
	return res;
}
