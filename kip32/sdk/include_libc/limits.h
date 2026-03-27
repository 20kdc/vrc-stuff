#pragma once

/*
 * kip32 minimal incomplete libc
 */

/*
 * MicroPython assumes SSIZE_MIN/SSIZE_MAX are here.
 */

#include_next <limits.h>

#define SSIZE_MIN LONG_MIN
#define SSIZE_MAX LONG_MAX
