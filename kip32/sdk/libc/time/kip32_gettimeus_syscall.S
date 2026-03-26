.section .text.__kip32_gettimeus, "ax"
.align 2
.option norelax
.globl __kip32_gettimeus
.type __kip32_gettimeus, @function

__kip32_gettimeus:
	jal __kip32_gettimeus_syscall
	ret

.section .kip32_metadata.__kip32_gettimeus_syscall, "aS"
/* truncation errors can occur if this isn't given */
.align 1

__kip32_gettimeus_syscall:
	.string "syscall:stdsyscall_getutctime"
