/*
 * Optimized-ish abs()/labs()
 * Accounts for emulation speed and merges the definitions
 * Could use an intrinsic on Udon but not worth the trouble
 */

.section .text.abs, "ax"
.align 2
.globl abs
.globl labs
.type abs, @function
.type labs, @function

abs:
labs:
	bge a0, zero, .L1
	sub a0, zero, a0
.L1:
	ret
