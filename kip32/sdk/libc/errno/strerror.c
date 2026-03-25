#include <errno.h>
#include <string.h>

char * strerror(int errnum) {
	if (errnum == ENOMEM)
		return (char *) (const char *) "Out of memory";
	if (errnum == EDOM)
		return (char *) (const char *) "Numerical argument out of domain";
	if (errnum == ERANGE)
		return (char *) (const char *) "Argument out of range";
	if (errnum == EILSEQ)
		return (char *) (const char *) "Invalid or incomplete multibyte or wide character";
	return (char *) (const char *) "Unknown error number";
}
