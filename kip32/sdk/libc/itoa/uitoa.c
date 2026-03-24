#include <stdlib.h>

#define ITOA_TYPE unsigned int
#define ITOA_SYM uitoa

#include "itoa.h"

char * ultoa(unsigned long value, char * str, int radix) __attribute__((alias("uitoa")));
