#pragma once

/*
 * kip32 minimal incomplete libc
 */

/* Standard-required errors */
#define EDOM 33
#define ERANGE 34
#define EILSEQ 84

/* Other (used by i.e. sbrk) */
#define ENOMEM 12

extern int errno;
