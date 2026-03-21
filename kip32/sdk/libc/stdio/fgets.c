#include <stdio.h>

char * fgets(char * restrict s, int n, FILE * restrict stream) {
	/* We're not 'supposed' to clear the error flag. Put it back where we found it when we're done. */
	int backupFlags = stream->flags & __KIP32_LIBC_FILEFLAG_ERROR;
	stream->flags &= ~__KIP32_LIBC_FILEFLAG_ERROR;
	/*
	 * The spec is a bit ambiguous for values of n < 1. cppreference reports n < 1 is an error.
	 * n == 1 is a bit clearer, minus the 'last character read' bit; we should probably place a null byte here.
	 * The wording there is a bit awkward, but indicates a null byte is *always* written, so here's our interpretation:
	 * For n <= 0, we place a null byte that we 'don't have room for'.
	 * For n = 1, we skip the read, but we didn't hit EOF, so we place a null byte and 'succeed'.
	 */
	if (n < 1) {
		stream->flags |= __KIP32_LIBC_FILEFLAG_ERROR;
		return NULL;
	}
	/* subtract 1 here to ensure we have room for the null terminator */
	n--;
	size_t idx = 0;
	while (idx < n) {
		int c = fgetc(stream);
		if (c == EOF) {
			if (idx == 0 || (stream->flags & __KIP32_LIBC_FILEFLAG_ERROR)) {
				/* Read error, or end of file w/ no read characters */
				stream->flags |= backupFlags;
				return NULL;
			} else {
				break;
			}
		} else {
			s[idx++] = c;
			if (c == '\n')
				break;
		}
	}
	/* success */
	s[idx] = 0;
	stream->flags |= backupFlags;
	return s;
}
