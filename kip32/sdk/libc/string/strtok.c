#include <string.h>

static char * strtok_state;

char * strtok(char * __restrict__ haystack, const char * __restrict__ needles) {
	if (!haystack)
		haystack = strtok_state;
	strtok_state = NULL;
	/* terminated? */
	if (!haystack)
		return NULL;
	/* step 3 */
	haystack += strspn(haystack, needles);
	if (!*haystack)
		return NULL;
	/* step 4 */
	size_t token_len = strcspn(haystack, needles);
	if (haystack[token_len]) {
		haystack[token_len] = 0;
		strtok_state = haystack + token_len + 1;
	}
	/* else last token, leave strtok_state == NULL */
	return haystack;
}
