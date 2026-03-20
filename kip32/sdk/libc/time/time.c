#include <time.h>

time_t time(time_t * timer) {
	struct timespec ts = {};
	timespec_get(&ts, TIME_UTC);
	if (timer)
		*timer = ts.tv_sec;
	return ts.tv_sec;
}
