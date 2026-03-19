/*
 * This file is designed to execute in KIP32 and in userspace QEMU.
 * It provides access to all RV32 M extension instructions as function calls for comprehensive testing.
 */

.text

.option push
.option arch, +m2p0

.globl muldiv_mul
muldiv_mul:
	mul a0, a0, a1
	ret

.globl muldiv_mulh
muldiv_mulh:
	mulh a0, a0, a1
	ret

.globl muldiv_mulhsu
muldiv_mulhsu:
	mulhsu a0, a0, a1
	ret

.globl muldiv_mulhu
muldiv_mulhu:
	mulhu a0, a0, a1
	ret

.globl muldiv_div
muldiv_div:
	div a0, a0, a1
	ret

.globl muldiv_divu
muldiv_divu:
	divu a0, a0, a1
	ret

.globl muldiv_rem
muldiv_rem:
	rem a0, a0, a1
	ret

.globl muldiv_remu
muldiv_remu:
	remu a0, a0, a1
	ret

.option pop
