#include <string.h>

void * memset(void * s, int c, size_t n) {
	void * target = s;
	/* bootstrap up to 8 bytes */
	size_t already = 0;
	if (n >= (sizeof(int) * 2)) {
		((int *) target)[0] = 0;
		((int *) target)[1] = 0;
		already = sizeof(int) * 2;
	} else if (n >= sizeof(int)) {
		((int *) target)[0] = 0;
		already = sizeof(int);
	} else if (n > 0) {
		((char *) target)[0] = 0;
		already = sizeof(char);
	} else {
		return s;
	}
	/* fill-in. this will naturally exponentially double and then likely fill the rest in quickly */
	while (already < n) {
		size_t amount = n - already;
		if (amount > already)
			amount = already;
		memcpy(s + already, s, amount);
		already += amount;
	}
	return s;
}
