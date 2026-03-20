#pragma once

/*
 * kip32 minimal incomplete libc
 */

#include <stddef.h>

/* memmove_syscall.S */
void * memcpy(void * __restrict__ dest, const void * __restrict__ src, size_t n);
void * memmove(void * dest, const void * src, size_t n);
