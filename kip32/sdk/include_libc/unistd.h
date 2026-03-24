#pragma once

/*
 * kip32 minimal incomplete libc
 */

/* MicroPython includes this for ssize_t. */

typedef signed long ssize_t;

/* MicroPython expects the SEEK_* constants. */
#include <stdio.h>
