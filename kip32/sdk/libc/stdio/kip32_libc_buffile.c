#include <stdio.h>
#include <string.h>

typedef struct _KIP32_LIBC_BUFFILE BUFFILE;

/*
 * In-memory stream so sscanf/sprintf can work.
 */

static int libc_buffile_getc(FILE * f) {
	BUFFILE * bf = (BUFFILE *) f;
	if (bf->pos >= bf->len)
		return EOF;
	return bf->buf[bf->pos++];
}

static int libc_buffile_putc(int c, FILE * f) {
	BUFFILE * bf = (BUFFILE *) f;
	if (bf->pos >= bf->len)
		return EOF;
	bf->buf[bf->pos++] = c;
	return c & 0xFF;
}

static size_t libc_buffile_read(void * restrict ptr, size_t size, FILE * f) {
	BUFFILE * bf = (BUFFILE *) f;
	size_t remain = bf->len - bf->pos;
	if (remain < size)
		size = remain;
	memcpy(ptr, bf->buf + bf->pos, size);
	bf->pos += size;
	return size;
}

static size_t libc_buffile_write(const void * restrict ptr, size_t size, FILE * f) {
	BUFFILE * bf = (BUFFILE *) f;
	size_t remain = bf->len - bf->pos;
	if (remain < size)
		size = remain;
	memcpy(bf->buf + bf->pos, ptr, size);
	bf->pos += size;
	return size;
}


struct _KIP32_LIBC_BUFFILE __kip32_libc_buffile(char * buffer, size_t pos, size_t len) {
	return (struct _KIP32_LIBC_BUFFILE) {
		.base = {
			.flags = 0,
			.getc = libc_buffile_getc,
			.putc = libc_buffile_putc,
			.read = libc_buffile_read,
			.write = libc_buffile_write
		},
		.buf = (unsigned char *) buffer,
		.pos = pos,
		.len = len
	};
}
