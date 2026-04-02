#include <kip32.h>
#include <stdio.h>

// _start here is, as per the name, run on startup.
KIP32_EXPORT void _start() {
	// Since this is fputs, we must manually append a newline.
	fputs("Hello from kip32 world!\n", stderr);
}

// __kip32_libc_systemout is an internal implementation of the FILE interface.
// This implementation is intended solely to support debug output.
// If you want to use __kip32_libc_systemout, it's good practice to declare it
//  this way, as stderr. This helps with keeping code relatively portable.
FILE * stderr = &__kip32_libc_systemout;
