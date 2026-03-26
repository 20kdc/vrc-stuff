#include <time.h>

static struct tm gmtime_res;

#define HOUR_SECONDS (60 * 60)
#define DAY_SECONDS (24 * HOUR_SECONDS)

/*
 * Is leap year? (works with 1900-relative years)
 */
static inline int is_leap_year(int year) {
	year += 1900;
	/* if it's not divisible by 4, it's never a leap year */
	if (year & 3)
		return 0;
	/* if it's divisible by 4, but not 100, it's a leap year */
	if (year % 100)
		return 1;
	/* it's divisible by 4 and 100, can only be saved if it's divisible by 400 */
	return (year % 400) == 0;
}

static int year_month_days(int year, int month) {
	switch (month) {
		case 0:
			return 31;
		case 1:
			return is_leap_year(year) ? 29 : 28;
		case 2:
			return 31;
		case 3:
			return 30;
		case 4:
			return 31;
		case 5:
			return 30;
		case 6:
			return 31;
		case 7:
			return 31;
		case 8:
			return 30;
		case 9:
			return 31;
		case 10:
			return 30;
		case 11:
			return 31;
		default:
			/* uhoh */
			return 1000000;
	}
}

/*
 * Infers year/month/day/wday/yday from (deliberately out of range) tm_yday.
 */
static void infer_ymd_from_yday(struct tm * target) {
	/* wday is the easiest part, coming from a pretty clean derivation here */
	target->tm_wday = target->tm_yday + 4;
	target->tm_wday = target->tm_wday - ((target->tm_wday / 7) * 7);
	/* and now things get ugly */
	target->tm_year = 70;
	if (target->tm_yday >= 0) {
		/* tick through years */
		while (1) {
			int year_days = is_leap_year(target->tm_year) ? 366 : 365;
			if (target->tm_yday >= year_days) {
				target->tm_yday -= year_days;
				target->tm_year++;
			} else {
				break;
			}
		}
	} else while (target->tm_yday < 0) {
		/* go backwards! */
		int year_days = is_leap_year(target->tm_year - 1) ? 366 : 365;
		target->tm_yday += year_days;
		target->tm_year--;
	}
	/*
	 * tm_yday is set. tick through months
	 * tm_mday is temporarily 0-based for this code
	 */
	target->tm_mon = 0;
	target->tm_mday = target->tm_yday;
	while (1) {
		int month_days = year_month_days(target->tm_year, target->tm_mon);
		if (target->tm_mday >= month_days) {
			target->tm_mday -= month_days;
			target->tm_mon++;
		} else {
			break;
		}
	}
	/* this is 1-based */
	target->tm_mday++;
}

struct tm * gmtime(const time_t * timer) {
	/* setup this table with initial values*/
	time_t src = *timer;
	gmtime_res.tm_isdst = 0;
	time_t days = src / DAY_SECONDS;
	src -= days * DAY_SECONDS;
	gmtime_res.tm_yday = days;
	time_t hours = src / HOUR_SECONDS;
	src -= hours * HOUR_SECONDS;
	gmtime_res.tm_hour = hours;
	time_t minutes = src / 60;
	src -= minutes * 60;
	gmtime_res.tm_min = minutes;
	gmtime_res.tm_sec = src;
	infer_ymd_from_yday(&gmtime_res);
	return &gmtime_res;
}
struct tm * localtime(const time_t * timer) __attribute__((alias("gmtime")));
