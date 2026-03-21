#include <inttypes.h>

#define STRTOL_TYPE long long
#define STRTOL_SYM strtoll

#include "strtol.h"

intmax_t strtoimax(const char * __restrict__ nptr, char ** __restrict__ endptr, int base) __attribute__((alias("strtoll")));
