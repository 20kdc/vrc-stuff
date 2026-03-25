#include <stdlib.h>
#include <stdio.h>

void __assert_fail(const char * exprtext, const char * file, int line, const char * func) {
	fprintf(stderr, "assertion failed: `%s` in %s @ %s:%i\n", exprtext, func, file, line);
	abort();
}
