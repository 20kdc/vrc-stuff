#pragma once

/*
 * kip32 minimal incomplete libc
 */

/* REQUIRED for size_t, NULL per c11_n1570 7.22.{2,3} */
#include <stddef.h>

#define RAND_MAX 0x7FFFFFFF

/* atof + the strto{d/f/ld} functions are omitted because they involve floating-point */

int atoi(const char * nptr);
long atol(const char * nptr);
long long atoll(const char * nptr);

long strtol(const char * __restrict__ nptr, char ** __restrict__ endptr, int base);
long long strtoll(const char * __restrict__ nptr, char ** __restrict__ endptr, int base);
unsigned long strtoul(const char * __restrict__ nptr, char ** __restrict__ endptr, int base);
unsigned long long strtoull(const char * __restrict__ nptr, char ** __restrict__ endptr, int base);

/*
 * These are an 'informal standard' kept around mostly in Microsoft and IBM compilers.
 * However, they're a useful primitive to keep around for use in the formatting functions.
 * Beware that a signed hexadecimal conversion won't act like "%x", because "%x" forces unsigned.
 */

/* [negative symbol] + [max size for base 2] + null */
#define __KIP32_LIBC_ITOA_BUFSIZE_FN(type) ((sizeof(type) * 8) + 2)

#define __KIP32_LIBC_ITOA_BUFSIZE __KIP32_LIBC_ITOA_BUFSIZE_FN(int)
#define __KIP32_LIBC_UITOA_BUFSIZE __KIP32_LIBC_ITOA_BUFSIZE

#define __KIP32_LIBC_LTOA_BUFSIZE __KIP32_LIBC_ITOA_BUFSIZE_FN(long)
#define __KIP32_LIBC_ULTOA_BUFSIZE __KIP32_LIBC_LTOA_BUFSIZE

#define __KIP32_LIBC_LLTOA_BUFSIZE __KIP32_LIBC_ITOA_BUFSIZE_FN(long long)
#define __KIP32_LIBC_ULLTOA_BUFSIZE __KIP32_LIBC_LLTOA_BUFSIZE

char * itoa(int value, char * str, int radix);
char * uitoa(unsigned int value, char * str, int radix);
char * ltoa(long value, char * str, int radix);
char * ultoa(unsigned long value, char * str, int radix);
char * lltoa(long long value, char * str, int radix);
char * ulltoa(unsigned long long value, char * str, int radix);

/* 'Random' number generation. */
int rand();
void srand(unsigned int seed);

/* Memory management functions. */
/* void * aligned_alloc(size_t alignment, size_t size); -- is not implemented here */
void * calloc(size_t nmemb, size_t size);
void free(void * ptr);
void * malloc(size_t size);
void * realloc(void * ptr, size_t size);

/*
 * This calls system to print an error, and then performs an invalid memory access.
 */
_Noreturn void abort();

/*
 * On kip32, this writes text to the debug port.
 * A dedicated function could have been used, but this was neater.
 * On qemu, this is undefined, but it's assumed to do the same thing.
 */
int system(const char * string);

/*
 * Binary search through base.
 * Something I'll go out of my way to note is that the specification is pretty exact, and this function therefore has a number of interesting 'quirks'.
 * 1. compar is called with the key and an array element, in that order -- that is, (key, pivot).
 *    This means key and pivot can be of separate types.
 * 2. While this isn't explicitly allowed ('objects' is used), since we know where 'pivot' is going to be, pivot doesn't even need to point to real memory, though a non-NULL pointer should be used.
 *    That is, an array that needs more complex access can be supplied in the key structure.
 */
void * bsearch(const void * key, const void * base, size_t nmemb, size_t size, int (*compar)(const void * key, const void * pivot));

void qsort(void * base, size_t nmemb, size_t size, int (*compar)(const void * a, const void * b));

/* Is long the same as long int? Apparently! */

int abs(int j);
long labs(long j);
long long llabs(long long j);

/*
 * The various divs.
 * Note that div.c uses function aliasing and thus assumes div_t and ldiv_t are the same struct.
 */

typedef struct _div_t {
	int quot;
	int rem;
} div_t;

typedef struct _ldiv_t {
	long quot;
	long rem;
} ldiv_t;

typedef struct _lldiv_t {
	long long quot;
	long long rem;
} lldiv_t;

div_t div(int numer, int denom);
ldiv_t ldiv(long numer, long denom);
lldiv_t lldiv(long long numer, long long denom);
