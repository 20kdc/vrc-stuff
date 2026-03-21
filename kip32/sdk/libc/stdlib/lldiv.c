#include <stdlib.h>
#include <inttypes.h>

lldiv_t lldiv(long long numer, long long denom) {
	return (lldiv_t) {
		.quot = numer / denom,
		.rem = numer % denom
	};
}

imaxdiv_t imaxdiv(intmax_t numer, intmax_t denom) __attribute__((alias("lldiv")));
