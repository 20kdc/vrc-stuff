#include <stdlib.h>
#include <alloca.h>
#include <string.h>

/*
 * Name's implication is to use quicksort.
 * However, we're allowed to use whatever algorithm we want, in theory.
 *
 * To understand the following decisions, understand these principles:
 *
 * 1. In Udon, memmove is faster than basically any other operation, to the point where it's effectively instant.
 *    It's even faster than storing words!
 * 2. Conversely, swapping memory is incredibly slow.
 * 3. We can't rely on malloc.
 *
 * For that reason:
 * 1. Binary insertion sort is actually really good
 * 2. We use alloca to reserve room for a single element (important when doing the memmove).
 */
void qsort(void * base, size_t nmemb, size_t size, int (*compar)(const void * a, const void * b)) {
	/* Avoid wasting time on the alloca. */
	if (size <= 1)
		return;
	void * tempElement = alloca(size);
	/* Portion of the array that is already sorted. */
	size_t sortedElements = 1;
	while (sortedElements < nmemb) {
		void * newElement = base + (sortedElements * size);
		void * lastElement = newElement - size;
		if (compar(newElement, lastElement) >= 0) {
			/* newElement >= lastElement, so array remains sorted if we do this */
			sortedElements++;
			continue;
		}
		/* slowpath: Insert newElement. start with a binary search */
		size_t insertStart = 0;
		size_t insertEnd = sortedElements;
		while (insertStart != insertEnd) {
			size_t pivotIndex = ((insertStart + insertEnd) >> 1);
			void * pivot = base + (pivotIndex * size);
			if (compar(newElement, pivot) >= 0) {
				insertStart = pivotIndex + 1;
			} else {
				insertEnd = pivotIndex;
			}
		}
		void * insertPtr = base + (insertStart * size);
		/*
		 * We intend to insert at insertStart/insertEnd (they should be equal).
		 * Save the new element, shift everything forward (destroying the new element) and put it in.
		 */
		memcpy(tempElement, newElement, size);
		memmove(insertPtr + size, insertPtr, newElement - insertPtr);
		memcpy(insertPtr, tempElement, size);
		sortedElements++;
	}
}
