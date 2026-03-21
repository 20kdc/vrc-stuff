/*
 * kip32-optimized strlen
 * kip32 cost is *mainly* instruction by instruction
 */

.section .text.strlen, "ax"
.align 2
/* we don't want the syscall to be relaxed, if that's possible */
.option norelax
.globl strlen
.type strlen, @function

strlen:
	/*
	 * So we use some pretty crazy Udon-specific optimizations here.
	 * In particular, 32-bit unaligned reads are actually the fastest way to read data here.
	 * So we first do an unrolled check of the first three bytes, to ensure we have three readable bytes.
	 */
	lbu a1, 0(a0)
	beq a1, zero, .LR0
	lbu a1, 1(a0)
	beq a1, zero, .LR1
	lbu a1, 2(a0)
	beq a1, zero, .LR2
	/*
	 * We then proceed to the unaligned-read loop.
	 * Registers:
	 * a1: original a0 value - 2 (used for the final conversion into length)
	 * a2: word ; next byte is in upper 8 bits
	 * a3: 0x01000000, comparison value
	 */
	addi a1, a0, -2
	li a3, 0x01000000
.LLOOP:
	/* this core loop should become 3 extern calls */
	lw a2, 0(a0)
	addi a0, a0, 1
	bgeu a2, a3, .LLOOP
	/*
	 * turn a0 into length
	 * assuming immediate failure:
	 * a0 = str + 1
	 * a1 = str - 2
	 * we know str[0,1,2] are non-zero and str[3] was being checked, so expected result is 3
	 * so calc a0 - a1
	 */
	sub a0, a0, a1
	ret
.LR0:
	addi a0, zero, 0
	ret
.LR1:
	addi a0, zero, 1
	ret
.LR2:
	addi a0, zero, 2
	ret
