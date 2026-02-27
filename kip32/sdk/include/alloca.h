#pragma once
/* Hyperminimalist implementation, keeping in mind we're hard-committed to compilers that use intrinsics for this. */
#include <stddef.h>
#define alloca(size) __builtin_alloca(size)
