/*
 * This file provides 'reference data generation code' running in QEMU with a decently clean way to perform syscalls.
 */

#include <stddef.h>

typedef signed long ssize_t;

ssize_t write(int fd, const void * buf, size_t sz);
void _exit(int v);
