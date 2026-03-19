/*
 * This file provides 'reference data generation code' running in QEMU with a decently clean way to perform syscalls.
 */

.text
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
