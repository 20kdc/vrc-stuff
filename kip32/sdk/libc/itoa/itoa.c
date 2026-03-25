#include <stdlib.h>
#include <limits.h>

#define ITOA_TYPE int
#define ITOA_SYM itoa
#define ITOA_UNSIGNED_SYM uitoa
#define ITOA_UNSIGNED_TYPE unsigned int

#include "itoa.h"

char * ltoa(long value, char * str, int radix) __attribute__((alias("itoa")));
