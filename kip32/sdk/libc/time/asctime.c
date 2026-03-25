#include <time.h>
#include <string.h>

/*
 * 3: day of week
 * 1: space
 * 3: month
 * 1: space
 * 2: day of month
 * 1: space
 * 8: hms
 * 1: space
 * 4: year
 * 1: newline
 * 1: null
 */
#define ASCTIME_LEN 26
static char asctime_buf[ASCTIME_LEN];

char * asctime(const struct tm * timeptr) {
	strftime(asctime_buf, ASCTIME_LEN, "%a %b %d %T %Y\n", timeptr);
	asctime_buf[ASCTIME_LEN - 1] = 0;
	return asctime_buf;
}
