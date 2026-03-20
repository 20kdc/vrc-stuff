#pragma once

/*
 * kip32 minimal incomplete libc
 */

/* REQUIRED for size_t, NULL per c11_n1570 7.21.1 */
#include <stddef.h>

/* For the targets we're interested in, 4GiB is an impossible luxury. */
typedef size_t fpos_t;

/*
 * In this libc, FILE is not opaque.
 * This is because we assume the application will create most FILEs (and will thus implement fopen, freopen, buffering if desired, etc.)
 * FILE is assumed to be a 'base type' that can be embedded in other types.
 *
 * The main goal for implementing FILE in this libc is for the implementation of the scanf and printf series.
 */
typedef struct _FILE {
	/* ungetc buffer. Initialize to EOF. ungetc handling is managed automatically. */
	int ungetc;
	/* Read. Note we don't bother with the X*Y here. */
	size_t (*read)(void * __restrict__ ptr, size_t size, struct _FILE * __restrict__ stream);
	/* Write. Note we don't bother with the X*Y here. */
	size_t (*write)(const void * __restrict__ ptr, size_t size, struct _FILE * __restrict__ stream);
} FILE;

/* Constants */
#define EOF (-1)

/* Not really provided by the libc. */
extern FILE * stdin;
extern FILE * stdout;
extern FILE * stderr;

size_t fread(void * __restrict__ ptr, size_t size, size_t nmemb, FILE * __restrict__ stream);
size_t fwrite(const void * __restrict__ ptr, size_t size, size_t nmemb, FILE * __restrict__ stream);

/*
 * These functions are not actually provided by the libc.
 * Their definitions are provided here so that application code may provide an appropriate implementation.
 */

int fclose(FILE * stream);
int fflush(FILE * stream);
