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
	/*
	 * actual time
	 * key caveat: year is relative to 1900, so is 70 for 1970!
	 * most other fields are 0-based, except tm_mday which is 1-based
	 */
	int tm_sec, tm_min, tm_hour, tm_mday, tm_mon, tm_year;
	/* the DST flag */
	int tm_isdst;
	/*
	 * calculated.
	 * wday is 0 for Sunday and goes up to 6 for Saturday.
	 * yday is 0 for 1st January.
	 */
	int tm_wday, tm_yday;
};

clock_t clock(); /* always fails */
time_t time(time_t * timer);
/* Used to implement timespec/etc. Might not be implemented! */
long long __kip32_gettimeus();
int timespec_get(struct timespec * ts, int base);

char * asctime(const struct tm * timeptr);
char * ctime(const time_t * timer);
size_t strftime(char * __restrict__ s, size_t maxsize, const char * __restrict__ format, const struct tm * __restrict__ timeptr); /* NYI */

/* 'model of time' functions -- all live in gmtime.c */
time_t mktime(struct tm * timeptr); /* NYI */
struct tm * gmtime(const time_t * timer);
struct tm * localtime(const time_t * timer);
