#include <time.h>
#include <unistd.h>

long long __kip32_gettimeus() {
	struct __clock_gettime64_timespec ts;
	clock_gettime64(CLOCK_REALTIME, &ts);
	return (ts.seconds * 1000000L) + (long long) (ts.nanoseconds / 1000);
}
