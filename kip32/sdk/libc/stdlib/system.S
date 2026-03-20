/*
 * 'system' here writes messages to what is effectively stderr
 */

.section .text.system, "ax"
.align 2
.option norelax
.globl system
.type system, @function

system:
	beq a0, zero, .RET
	mv a1, a0
.TOP:
	lbu a0, 0(a1)
	beq a0, zero, .RET
	jal __kip32_system_putchar_syscall
	addi a1, a1, 1
	j .TOP
.RET:
	ret

.section .kip32_metadata.__kip32_system_putchar_syscall, "aS"

__kip32_system_putchar_syscall:
	.string "syscall:stdsyscall_putchar"
