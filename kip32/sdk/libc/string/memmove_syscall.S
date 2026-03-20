/*
 * Syscall memcpy
 */

.section .text.__kip32_syscall_memmove, "ax"
.align 2
/* we don't want the syscall to be relaxed, if that's possible */
.option norelax
.globl __kip32_syscall_memmove
.type __kip32_syscall_memmove, @function

__kip32_syscall_memmove:
	call __kip32_syscall_memmove_syscall
	ret

.section .kip32_metadata.__kip32_syscall_memmove_syscall, "aS"

__kip32_syscall_memmove_syscall:
	.string "syscall:stdsyscall_memmove"
