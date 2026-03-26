/*
 * Since it is reasonably pointless to implement setjmp without longjmp or vice versa, they are kept as one file for ease of understanding.
 */

.section .text.setjmp, "ax"
.align 2
.globl _setjmp
.type _setjmp, @function

_setjmp:
	sw ra,  0x00(a0)
	sw sp,  0x04(a0)
	sw s0,  0x08(a0)
	sw s1,  0x0C(a0)
	sw s2,  0x10(a0)
	sw s3,  0x14(a0)
	sw s4,  0x18(a0)
	sw s5,  0x1C(a0)
	sw s6,  0x20(a0)
	sw s7,  0x24(a0)
	sw s8,  0x28(a0)
	sw s9,  0x2C(a0)
	sw s10, 0x30(a0)
	sw s11, 0x34(a0)
	/* first return */
	li a0, 0
	ret

.section .text.longjmp, "ax"
.align 2
.globl longjmp
.type longjmp, @function

longjmp:
	lw ra,  0x00(a0)
	lw sp,  0x04(a0)
	lw s0,  0x08(a0)
	lw s1,  0x0C(a0)
	lw s2,  0x10(a0)
	lw s3,  0x14(a0)
	lw s4,  0x18(a0)
	lw s5,  0x1C(a0)
	lw s6,  0x20(a0)
	lw s7,  0x24(a0)
	lw s8,  0x28(a0)
	lw s9,  0x2C(a0)
	lw s10, 0x30(a0)
	lw s11, 0x34(a0)
	/* second return */
	mv a0, a1
	/* 7.13.2.1.4 'longjmp function cannot cause the setjmp macro to return the value 0' etc. */
	bne a0, zero, .RET
	li a0, 1
.RET:
	ret
