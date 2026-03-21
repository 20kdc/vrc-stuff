#include <stdlib.h>
#include <stdint.h>

static int64_t rand_seed;

int rand() {
	rand_seed *= 0x5DEECE66DL;
	rand_seed += 0xB;
	rand_seed &= 0xFFFFFFFFFFFFL;
	/*
	 * This is chosen for ease of cross-testing; it exactly matches java.util.Random except with the top bit masked off.
	 * This assures (for better or worse) that it is identical to that generator.
	 */
	return ((int) (rand_seed >> 16)) & 0x7FFFFFFF;
}

void srand(unsigned int seed) {
	/* masked by default because expansion of uint32 to int64 */
	rand_seed = (int64_t) (seed ^ 0x5DEECE66DL);
}
