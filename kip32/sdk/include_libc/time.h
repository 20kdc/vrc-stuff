#pragma once

/*
 * kip32 minimal incomplete libc
 */

/* you guessed it -- spec wants NULL! */
#include <stddef.h>

#define CLOCKS_PER_SEC 1
#define TIME_UTC 1

typedef long long clock_t;
typedef long long time_t;

struct timespec {
	time_t tv_sec;
	long tv_nsec;
};

clock_t clock();
time_t time(time_t * timer);
int timespec_get(struct timespec * ts, int base); /* NYI */
