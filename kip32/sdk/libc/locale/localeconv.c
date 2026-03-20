#include <locale.h>

#define CSTR(v) ((char *) (const char *) (v))

static const struct lconv lconv_data = {
	.decimal_point = CSTR("."),
	.thousands_sep = CSTR(""),
	.grouping = CSTR(""),
	.mon_decimal_point = CSTR(""),
	.mon_thousands_sep = CSTR(""),
	.mon_grouping = CSTR(""),
	.positive_sign = CSTR(""),
	.negative_sign = CSTR(""),
	.currency_symbol = CSTR(""),
	.int_curr_symbol = CSTR(""),
};

struct lconv * localeconv() {
	return (struct lconv *) &lconv_data;
}
