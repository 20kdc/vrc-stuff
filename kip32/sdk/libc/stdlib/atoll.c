#include <stdlib.h>
#include <errno.h>

long long atoll(const char * nptr) {
	int olderr = errno;
	long long val = strtoll(nptr, NULL, 10);
	errno = olderr;
	return val;
}
