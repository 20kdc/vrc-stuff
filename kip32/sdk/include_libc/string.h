#pragma once

/*
 * kip32 minimal incomplete libc
 */

#include <stddef.h>

void * memcpy(void * __restrict__ dest, const void * __restrict__ src, size_t n);
void * memmove(void * dest, const void * src, size_t n);

char * strcpy(char * __restrict__ dest, const char * __restrict__ src);

void * memset(void * s, int c, size_t n);
size_t strlen(const char * s);
