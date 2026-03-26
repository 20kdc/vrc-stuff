#include <stdint.h>
#include "lengthflags.h"

int __kip32_libc_lengthflags_tryconsume(int * lengthFlags, char chr) {
	if (chr == 'l') { /* length modifiers */
		*lengthFlags |= ((*lengthFlags) & LENGTH_FLAGS_L1) ? LENGTH_FLAGS_L2 : LENGTH_FLAGS_L1;
	} else if (chr == 'h') {
		*lengthFlags |= ((*lengthFlags) & LENGTH_FLAGS_H1) ? LENGTH_FLAGS_H2 : LENGTH_FLAGS_H1;
	} else if (chr == 'j') {
		*lengthFlags |= LENGTH_FLAGS_J;
	} else if (chr == 'z') {
		*lengthFlags |= LENGTH_FLAGS_Z;
	} else if (chr == 't') {
		*lengthFlags |= LENGTH_FLAGS_T;
	} else if (chr == 'L') {
		*lengthFlags |= LENGTH_FLAGS_LD;
	} else {
		return 0;
	}
	return 1;
}

void __kip32_libc_lengthflags_write(int lengthFlags, void * target, long long value) {
	if (lengthFlags & LENGTH_FLAGS_H2) {
		*((char *) target) = (char) value;
	} else if (lengthFlags & LENGTH_FLAGS_H1) {
		*((short *) target) = (short) value;
	} else if (lengthFlags & LENGTH_FLAGS_L2) {
		*((long long *) target) = (long long) value;
	} else if (lengthFlags & LENGTH_FLAGS_J) {
		*((intmax_t *) target) = (intmax_t) value;
	} else {
		/* LD makes no sense here. note L1/Z/T are ignored b/c they resolve to int-ish anyway */
		*((int *) target) = (int) value;
	}
}
