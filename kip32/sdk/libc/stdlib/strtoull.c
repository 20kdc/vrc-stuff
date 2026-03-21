#include <inttypes.h>

#define STRTOL_TYPE unsigned long long
#define STRTOL_SYM strtoull

#include "strtol.h"

uintmax_t strtoumax(const char * __restrict__ nptr, char ** __restrict__ endptr, int base) __attribute__((alias("strtoull")));
