#include <stdlib.h>

void * bsearch(const void * key, const void * base, size_t nmemb, size_t size, int (*compar)(const void * key, const void * pivot)) {
	/*
	 * Simple binary search routine.
	 * Not much to say here, we loop until we have no remaining elements.
	 */
	while (nmemb) {
		size_t pivot_index = nmemb >> 1;
		const void * pivot = base + (size * pivot_index);
		/* >0: key > pivot ; <0: key < pivot */
		int c = compar(key, pivot);
		if (c == 0) {
			/* This conversion from const to non-const is inherent in the specification. */
			return (void *) pivot;
		} else if (c > 0) {
			/* latter half, not including pivot */
			base = pivot + size;
			nmemb -= pivot_index + 1;
		} else {
			nmemb = pivot_index;
		}
	}
	return NULL;
}
