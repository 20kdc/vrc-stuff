/*
 * Fake sbrk.
 * Turns out brk didn't work on QEMU for some reason, so we have a fake sbrk instead.
 */

#include <unistd.h>
#include <errno.h>

#define SBRK_WORDS 0x1000000
static int sbrk_storage[SBRK_WORDS];

static void * data_segment_end = sbrk_storage;

void * sbrk(intptr_t increment) {
	void * new_end = data_segment_end + increment;
	if (new_end != data_segment_end) {
		uintptr_t storage_start = (uintptr_t) (sbrk_storage);
		uintptr_t storage_end = (uintptr_t) (sbrk_storage + SBRK_WORDS);
		uintptr_t new_end_u = (uintptr_t) (new_end);
		if (new_end_u < storage_start || new_end_u > storage_end) {
			errno = ENOMEM;
			return (void *) -1;
		}
		data_segment_end = new_end;
	}
	return data_segment_end;
}
