#pragma once

/*
 * kip32 minimal incomplete libc
 */

#include <stddef.h>

void * memcpy(void * __restrict__ dest, const void * __restrict__ src, size_t n);
void * memmove(void * dest, const void * src, size_t n);

char * strcpy(char * __restrict__ dest, const char * __restrict__ src);
char * strncpy(char * __restrict__ dest, const char * __restrict__ src, size_t n);

char * strcat(char * __restrict__ dest, const char * __restrict__ src);
char * strncat(char * __restrict__ dest, const char * __restrict__ src, size_t n);

int memcmp(const void * a, const void * b, size_t n);
int strcmp(const char * a, const char * b);
int strcoll(const char * a, const char * b);
int strncmp(const char * a, const char * b, size_t n);
size_t strxfrm(char * __restrict__ s1, const char * __restrict__ s2, size_t n);

void * memchr(const void * s, int c, size_t n);
char * strchr(const char * s, int c);
size_t strcspn(const char * haystack, const char * stopchars); /* NYI */
char * strpbrk(const char * haystack, const char * needles); /* NYI */
char * strrchr(const char * s, int c);
size_t strspn(const char * haystack, const char * runchars); /* NYI */
char * strstr(const char * haystack, const char * needle);
char * strtok(char * __restrict__ haystack, const char * __restrict__ needles); /* NYI */

void * memset(void * s, int c, size_t n);
char * strerror(int errnum); /* NYI. File in errno/ */
size_t strlen(const char * s);
