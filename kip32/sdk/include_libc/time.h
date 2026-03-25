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

struct tm {
	int tm_sec, tm_min, tm_hour, tm_mday, tm_mon, tm_year, tm_wday, tm_yday, tm_isdst;
};

clock_t clock();
time_t time(time_t * timer);
int timespec_get(struct timespec * ts, int base); /* NYI */
char * asctime(const struct tm * timeptr);
size_t strftime(char * __restrict__ s, size_t maxsize, const char * __restrict__ format, const struct tm * __restrict__ timeptr); /* NYI */
