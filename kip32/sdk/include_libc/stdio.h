#pragma once

/*
 * kip32 minimal incomplete libc
 */

/* REQUIRED for size_t, NULL per c11_n1570 7.21.1 */
#include <stddef.h>

/* required for definitions of v-fmt-functions */
#include <stdarg.h>

/* For the targets we're interested in, 4GiB is an impossible luxury. */
typedef size_t fpos_t;

#define __KIP32_LIBC_FILEFLAG_UNGETC 0x100
#define __KIP32_LIBC_FILEFLAG_EOF 0x200
#define __KIP32_LIBC_FILEFLAG_ERROR 0x400
/* various code throughout stdio assumes this does not require shifting */
#define __KIP32_LIBC_FILEFLAG_UNGETC_CHAR 0xFF

/*
 * In this libc, FILE is not opaque.
 * This is because we assume the application will create most FILEs (and will thus implement fopen, freopen, buffering if desired, etc.)
 * FILE is assumed to be a 'base type' that can be embedded in other types.
 *
 * Note that:
 * 1. All the function pointers here must be set.
 * 2. fget* functions are defined as immediately returning if the EOF flag is set. This is handled automatically in the frontend.
 * 3. ungetc is handled automatically in the frontend.
 *
 * The main goal for implementing FILE in this libc is for the implementation of the scanf and printf series.
 */
typedef struct _FILE {
	/* ungetc buffer & flags. Initialize to 0. */
	int flags;
	/* getc. */
	int (*getc)(struct _FILE * __restrict__ stream);
	/* putc. */
	int (*putc)(int c, struct _FILE * __restrict__ stream);
	/* Read. Note we don't bother with the X*Y here. Beware: Not expected to handle ungetc. */
	size_t (*read)(void * __restrict__ ptr, size_t size, struct _FILE * __restrict__ stream);
	/* Write. Note we don't bother with the X*Y here. */
	size_t (*write)(const void * __restrict__ ptr, size_t size, struct _FILE * __restrict__ stream);
} FILE;

/*
 * Implements a 'buffer file'.
 * This does NOT implement any application-layer code (obviously), so be wary what you use this with.
 * It is intended to support the string scan/print functions.
 */
struct _KIP32_LIBC_BUFFILE {
	FILE base;
	/* Load-bearing unsignedness. */
	unsigned char * buf;
	/* Beware: Some calculations assume that pos <= len */
	size_t pos;
	size_t len;
};

struct _KIP32_LIBC_BUFFILE __kip32_libc_buffile(char * buffer, size_t pos, size_t len);

/* Constants */
#define EOF (-1)

/* Not really provided by the libc. */
extern FILE * stdin;
extern FILE * stdout;
extern FILE * stderr;

/*
 * 'Operations on files' / 'File access functions'
 * These functions are not actually provided by this libc.
 * Their definitions are provided here so that application code may provide an appropriate implementation.
 */

int fclose(FILE * stream);
int fflush(FILE * stream);

/* formatted -- interposers, f-series */
int fprintf(FILE * __restrict__ stream, const char * __restrict__ format, ...);
int fscanf(FILE * __restrict__ stream, const char * __restrict__ format, ...);
/* formatted -- interposers, stdio */
int printf(const char * __restrict__ format, ...);
int scanf(const char * __restrict__ format, ...);
/* formatted -- interposers, s-series, NYI */
int snprintf(char * __restrict__ s, size_t n, const char * __restrict__ format, ...);
int vsnprintf(char * __restrict__ s, size_t n, const char * __restrict__ format, va_list arg);
int sprintf(char * __restrict__ s, const char * __restrict__ format, ...);
int sscanf(const char * __restrict__ s, const char * __restrict__ format, ...);
/* formatted -- interposers, v-stdio, NYI */
int vprintf(const char * __restrict__ format, va_list arg);
int vscanf(const char * __restrict__ format, va_list arg);
/* formatted -- string, NYI */
int vsprintf(char * __restrict__ s, const char * __restrict__ format, va_list arg);
int vsscanf(const char * __restrict__ s, const char * __restrict__ format, va_list arg);
/* formatted -- core, NYI */
int vfprintf(FILE * __restrict__ stream, const char * __restrict__ format, va_list arg);
int vfscanf(FILE * __restrict__ stream, const char * __restrict__ format, va_list arg);

/* 'frontend' functions to FILE struct */
int fgetc(FILE * stream);
char * fgets(char * __restrict__ s, int n, FILE * __restrict__ stream);
int fputs(const char * __restrict__ s, FILE * __restrict__ stream);
int fputc(int c, FILE * stream);
int ungetc(int c, FILE * stream);

int getc(FILE * stream);
int putc(int c, FILE * stream);

size_t fread(void * __restrict__ ptr, size_t size, size_t nmemb, FILE * __restrict__ stream);
size_t fwrite(const void * __restrict__ ptr, size_t size, size_t nmemb, FILE * __restrict__ stream);

int getchar();
int putchar(int c);
int puts(const char * s);

/* Error-handling functions */
void clearerr(FILE * stream);
int feof(FILE * stream);
int ferror(FILE * stream);
/* NYI */
void perror(const char * s);
