#include <stdlib.h>

div_t div(int numer, int denom) {
	return (div_t) {
		.quot = numer / denom,
		.rem = numer % denom
	};
}

ldiv_t ldiv(long numer, long denom) __attribute__((alias("div")));
