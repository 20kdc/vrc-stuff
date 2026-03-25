#include <stdlib.h>

#define UITOA_TYPE unsigned int
#define UITOA_SYM uitoa

#include "uitoa.h"

char * ultoa(unsigned long value, char * str, int radix) __attribute__((alias("uitoa")));
