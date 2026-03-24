#include <stdlib.h>

#define ITOA_TYPE int
#define ITOA_SYM itoa

#include "itoa.h"

char * ltoa(long value, char * str, int radix) __attribute__((alias("itoa")));
