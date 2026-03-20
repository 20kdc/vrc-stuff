#include <stdlib.h>

/*
 * Because this works with long long, and because we don't need to do the multiple-symbol-alias thing like with abs, this is written in C.
 * This does mean the compiler attempts to prevent branching to a fault.
 */
long long llabs(long long j) {
	return (j < 0) ? -j : j;
}
