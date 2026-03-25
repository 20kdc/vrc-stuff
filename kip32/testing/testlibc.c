#include "testlibc.h"
#include <stdio.h>
#include <unistd.h>

/*
 * This provides a very simplistic implementation of kip32 stdio on FDs.
 * It might be useful as a template to build a more 'proper' unbuffered stdio implementation.
 */

struct fdfile {
	struct _FILE base;
	int fd;
};

static int fdfile_getc(FILE * f) {
	struct fdfile * bf = (struct fdfile *) f;
	unsigned char chr;
	ssize_t res = read(bf->fd, &chr, 1);
	if (res < 0) {
		f->flags |= __KIP32_LIBC_FILEFLAG_ERROR;
		return EOF;
	} else if (res == 0) {
		f->flags |= __KIP32_LIBC_FILEFLAG_EOF;
		return EOF;
	} else {
		return chr;
	}
}

static int fdfile_putc(int c, FILE * f) {
	struct fdfile * bf = (struct fdfile *) f;
	/* we shouldn't be ignoring errors like this but... */
	write(bf->fd, &c, 1);
	return c & 0xFF;
}

static size_t fdfile_read(void * restrict ptr, size_t size, FILE * f) {
	struct fdfile * bf = (struct fdfile *) f;
	unsigned char chr;
	size_t advance = 0;
	while (advance < size) {
		ssize_t res = read(bf->fd, ptr + advance, size - advance);
		if (res < 0) {
			f->flags |= __KIP32_LIBC_FILEFLAG_ERROR;
			break;
		} else if (res == 0) {
			f->flags |= __KIP32_LIBC_FILEFLAG_EOF;
			break;
		} else {
			advance += res;
		}
	}
	return advance;
}

static size_t fdfile_write(const void * restrict ptr, size_t size, FILE * f) {
	struct fdfile * bf = (struct fdfile *) f;
	unsigned char chr;
	size_t advance = 0;
	while (advance < size) {
		ssize_t res = write(bf->fd, ptr + advance, size - advance);
		if (res < 0) {
			f->flags |= __KIP32_LIBC_FILEFLAG_ERROR;
			break;
		} else if (res == 0) {
			f->flags |= __KIP32_LIBC_FILEFLAG_EOF;
			break;
		} else {
			advance += res;
		}
	}
	return advance;
}

#define FDFILE_BASE { \
	.flags = 0, \
	.getc = fdfile_getc, \
	.putc = fdfile_putc, \
	.write = fdfile_write, \
	.read = fdfile_read, \
}

static struct fdfile stdin_impl = {
	.base = FDFILE_BASE,
	.fd = STDIN_FILENO
};
static struct fdfile stdout_impl = {
	.base = FDFILE_BASE,
	.fd = STDOUT_FILENO
};
static struct fdfile stderr_impl = {
	.base = FDFILE_BASE,
	.fd = STDERR_FILENO
};

FILE * stdin = &stdin_impl.base;
FILE * stdout = &stdout_impl.base;
FILE * stderr = &stderr_impl.base;

int system(const char * text) {
	while (*text) {
		putchar(*text);
		text++;
	}
	return 0;
}

void puthex(int v) {
	putchar('0');
	putchar('x');
	for (int i = 0; i < 8; i++) {
		putchar(("0123456789ABCDEF")[(v >> 28) & 0xF]);
		v <<= 4;
	}
}

