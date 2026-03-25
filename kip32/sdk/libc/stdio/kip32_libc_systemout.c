/*
 * Implements stderr through the 'system()-as-stderr' mechanism.
 */

#include <stdio.h>
#include <stdlib.h>

static int stderr_getc(FILE * file) {
	file->flags |= __KIP32_LIBC_FILEFLAG_ERROR;
	return EOF;
}

static int stderr_putc(int c, FILE * file) {
	/* hacky code to ensure following bytes are 0 */
	c &= 0xFF;
	system((char *) &c);
	return c;
}

FILE __kip32_libc_systemout = {
	.flags = 0,
	.getc = stderr_getc,
	.putc = stderr_putc,
	.read = __kip32_libc_charfile_read,
	.write = __kip32_libc_charfile_write,
};
