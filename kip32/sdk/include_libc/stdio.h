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
 * 3. You are expected to set the EOF/error flags.
 * 4. ungetc is handled automatically in the frontend.
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

struct _KIP32_LIBC_BUFFILE {
	FILE base;
	/* Load-bearing unsignedness. */
	unsigned char * buf;
	/* Beware: Some calculations assume that pos <= len */
	size_t pos;
	size_t len;
};

/*
 * Implements a 'buffer file'.
 * This does NOT implement any application-layer code (obviously), so be wary what you use this with.
 * It is intended to support the string scan/print functions.
 * For this reason, it hides write errors.
 * Note that SIZE_MAX is passed to len in some unsafe cases (sprintf).
 */
struct _KIP32_LIBC_BUFFILE __kip32_libc_buffile(char * buffer, size_t pos, size_t len);

/* Constants */
#define _IOFBF 0
#define _IOLBF 1
#define _IONBF 2

#define BUFSIZ 256

#define EOF ((int) -1)

#define FOPEN_MAX 256
#define FILENAME_MAX 256
#define L_tmpnam FILENAME_MAX

#define SEEK_SET 0
#define SEEK_CUR 1
#define SEEK_END 2

/* Not really provided by the libc. */
extern FILE * stdin;
extern FILE * stdout;
extern FILE * stderr;

/*
 * 'Operations on files' / 'File access functions'
 * These functions are not actually provided by this libc.
 * Their definitions are provided here so that application code may provide an appropriate implementation.
 */

int remove(const char * filename);
int rename(const char * oldname, const char * newname);
FILE * tmpfile();
char * tmpname(char * s);

int fclose(FILE * stream);
int fflush(FILE * stream);
FILE * fopen(const char * __restrict__ filename, const char * __restrict__ mode);
FILE * freopen(const char * __restrict__ filename, const char * __restrict__ mode, FILE * __restrict__ stream);
void setbuf(FILE * __restrict__ stream, char * __restrict__ buf);
int setvbuf(FILE * __restrict__ stream, char * __restrict__ buf, int mode, size_t size);

/* formatted -- interposers */
int fprintf(FILE * __restrict__ stream, const char * __restrict__ format, ...);
int fscanf(FILE * __restrict__ stream, const char * __restrict__ format, ...);
int printf(const char * __restrict__ format, ...);
int scanf(const char * __restrict__ format, ...);
int snprintf(char * __restrict__ s, size_t n, const char * __restrict__ format, ...);
int sprintf(char * __restrict__ s, const char * __restrict__ format, ...);
int sscanf(const char * __restrict__ s, const char * __restrict__ format, ...);
int vprintf(const char * __restrict__ format, va_list arg);
int vscanf(const char * __restrict__ format, va_list arg);
int vsnprintf(char * __restrict__ s, size_t n, const char * __restrict__ format, va_list arg);
int vsprintf(char * __restrict__ s, const char * __restrict__ format, va_list arg);
int vsscanf(const char * __restrict__ s, const char * __restrict__ format, va_list arg);
/* formatted -- core */
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

int getchar();
int putchar(int c);
int puts(const char * s);

size_t fread(void * __restrict__ ptr, size_t size, size_t nmemb, FILE * __restrict__ stream);
size_t fwrite(const void * __restrict__ ptr, size_t size, size_t nmemb, FILE * __restrict__ stream);

/*
 * 'File positioning functions'
 * These functions are not actually provided by this libc.
 * Their definitions are provided here so that application code may provide an appropriate implementation.
 */

int fgetpos(FILE * __restrict__ stream, fpos_t * __restrict__ pos);
int fseek(FILE * stream, long offset, int whence);
int fsetpos(FILE * stream, const fpos_t * pos);
long ftell(FILE * stream);
void rewind(FILE * stream);

/* Error-handling functions */
void clearerr(FILE * stream);
int feof(FILE * stream);
int ferror(FILE * stream);
/* NYI */
void perror(const char * s);
