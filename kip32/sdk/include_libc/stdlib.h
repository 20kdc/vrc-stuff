#pragma once

/*
 * kip32 minimal incomplete libc
 */

/* REQUIRED for size_t, NULL per c11_n1570 7.22.{2,3} */
#include <stddef.h>

/*
 * On this libc, this writes text to the debug port.
 * A dedicated function could have been used, but this was neater.
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

/* The various divs. */

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
