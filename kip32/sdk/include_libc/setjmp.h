#pragma once

/*
 * kip32 minimal incomplete libc
 */

#define __JMPBUF_RA 0
#define __JMPBUF_SP 1
#define __JMPBUF_S0 2
#define __JMPBUF_SIZE 14

/*
 * Keep in mind that setjmp is a function call.
 * Therefore anything callee-saved that needs to be saved has already been saved by the compiler.
 * With that in mind, for a reasonable implementation, we must save:
 * ra
 * sp
 * s0-s11
 */
typedef long jmp_buf[__JMPBUF_SIZE];

int _setjmp(jmp_buf env) __attribute__((returns_twice));
#define setjmp(env) (_setjmp(env))

_Noreturn void longjmp(jmp_buf env, int val);
