/*
 * If you really want to stop the VM with an error *that badly...*
 * Beware: This implementation may be changed.
 * In Udon, it 'bricks' the UdonBehaviour and the responsible VM can no longer be reset or used.
 */

.section .text.abort, "ax"
.align 2
.globl abort
.type abort, @function

abort:
	la a0, __abort_msg
	call system
	sw zero, -4(zero)

.section .rodata.__abort_msg, "a"

__abort_msg:
	.string "*** kip32 abort ***\n"
