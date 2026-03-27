/*
 * This file provides test code and reference data generation code running in QEMU with a decently clean way to perform syscalls.
 */

.text

.globl read
read:
	li a7, 63
	ecall
	ret

.globl write
write:
	li a7, 64
	ecall
	ret

.globl _exit
_exit:
	li a7, 93
	ecall
	ret

.globl clock_gettime64
clock_gettime64:
	li a7, 403
	ecall
	ret
