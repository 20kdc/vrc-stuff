#pragma once

/*
 * This file is designed to execute in KIP32 and in userspace QEMU.
 * It provides access to all RV32 M extension instructions as function calls for comprehensive testing.
 */

int muldiv_mul(int a, int b);

int muldiv_mulh(int a, int b);
int muldiv_mulhsu(int a, int b);
int muldiv_mulhu(int a, int b);

int muldiv_div(int a, int b);
int muldiv_divu(int a, int b);
int muldiv_rem(int a, int b);
int muldiv_remu(int a, int b);
