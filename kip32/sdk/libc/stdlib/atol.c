#include <stdlib.h>
#include <errno.h>

long atol(const char * nptr) {
	int olderr = errno;
	long val = strtol(nptr, NULL, 10);
	errno = olderr;
	return val;
}

int atoi(const char * nptr) __attribute__((alias("atol")));
