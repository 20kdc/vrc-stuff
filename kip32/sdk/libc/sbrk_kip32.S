.section .text.sbrk, "ax"
.align 2
.option norelax
.globl sbrk
.type sbrk, @function

sbrk:
	jal __kip32_sbrk_syscall
	ret

.section .kip32_metadata.__kip32_sbrk_syscall, "aS"
/* truncation errors can occur if this isn't given */
.align 1

__kip32_sbrk_syscall:
	.string "syscall:stdsyscall_sbrk"
