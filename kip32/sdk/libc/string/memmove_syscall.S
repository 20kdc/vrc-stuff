/*
 * Syscall memcpy
 */

.section .text.__kip32_syscall_memmove, "ax"
.align 2
/* we don't want the syscall to be relaxed, if that's possible */
.option norelax
.globl memmove
.type memmove, @function
.globl memcpy
.type memcpy, @function

memmove:
memcpy:
	jal __kip32_syscall_memmove_syscall
	ret

.section .kip32_metadata.__kip32_syscall_memmove_syscall, "aS"
/* truncation errors can occur if this isn't given */
.align 1

__kip32_syscall_memmove_syscall:
	.string "syscall:stdsyscall_memmove"
