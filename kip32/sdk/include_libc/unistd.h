#pragma once

/*
 * kip32 minimal incomplete libc
 */

/* MicroPython expects the SEEK_* constants. */
#include <stdio.h>
/* Just in case. */
#include <limits.h>

/* for below syscalls */
#include <stddef.h>
#include <stdint.h>

/* MicroPython #includes this header for ssize_t. */
typedef signed long ssize_t;

/*
 * Linux syscall wrappers in linux_syscalls.S ; used by the appropriate compatibility code.
 * These do not update errno.
 */
#define STDIN_FILENO 0
#define STDOUT_FILENO 1
#define STDERR_FILENO 2
ssize_t read(int fd, const void * buf, size_t sz);
ssize_t write(int fd, const void * buf, size_t sz);
void _exit(int v);
#define CLOCK_REALTIME 0
struct __clock_gettime64_timespec {
	int64_t seconds;
	int32_t nanoseconds;
};
int clock_gettime64(int clockid, struct __clock_gettime64_timespec * tp);

/* Implemented in sbrk_kip32.c or sbrk_brk.c */
void * sbrk(intptr_t increment);
