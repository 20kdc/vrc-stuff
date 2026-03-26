#include <time.h>

int timespec_get(struct timespec * ts, int base) {
	if (base != TIME_UTC)
		return 0;
	/*
	 * > -1 // 1000
	 * -1
	 * > -1 - ((-1 // 1000) * 1000)
	 * 999
	 */
	long long microseconds = __kip32_gettimeus();
	long long seconds = microseconds / 1000000;
	microseconds -= (seconds * 1000000);
	ts->tv_sec = seconds;
	ts->tv_nsec = (long) (microseconds * 1000);
	return TIME_UTC;
}
