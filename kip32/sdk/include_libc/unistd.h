#pragma once

/*
 * kip32 minimal incomplete libc
 */

/* MicroPython expects the SEEK_* constants. */
#include <stdio.h>

/* for below syscalls */
#include <stddef.h>
#include <stdint.h>

/* MicroPython #includes this header for ssize_t. */
typedef signed long ssize_t;

/*
 * Linux syscall wrappers in linux_syscalls.S ; used by the appropriate compatibility code.
 * These do not update errno.
 */
ssize_t write(int fd, const void * buf, size_t sz);
void _exit(int v);

/* Implemented in sbrk_kip32.c or sbrk_brk.c */
void * sbrk(intptr_t increment);
