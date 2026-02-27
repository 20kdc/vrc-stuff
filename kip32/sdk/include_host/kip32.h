/* KIP-32 semihosting header */

#pragma once

#include <stdint.h>

#ifdef WINDOWS
#define KIP32_EXPORT __attribute__((dllexport))
#else
#define KIP32_EXPORT __attribute__((visibility("default")))
#endif

/* syscall emulator */
void kip32_syscall_emulator(const char * name, intptr_t * c0, intptr_t * c1, intptr_t * c2, intptr_t * c3, intptr_t * c4, intptr_t * c5, intptr_t * c6, intptr_t * c7);

#define KIP32_SYSCALL_CORE(name, e0, v0, e1, v1, e2, v2, e3, v3, e4, v4, e5, v5, e6, v6, e7, v7) \
{ \
	intptr_t a0 e0 v0; \
	intptr_t a1 e1 v1; \
	intptr_t a2 e2 v2; \
	intptr_t a3 e3 v3; \
	intptr_t a4 e4 v4; \
	intptr_t a5 e5 v5; \
	intptr_t a6 e6 v6; \
	intptr_t a7 e7 v7; \
	kip32_syscall_emulator(name, &a0, &a1, &a2, &a3, &a4, &a5, &a6, &a7); \
	v0 e0 a0; \
	v1 e1 a1; \
	v2 e2 a2; \
	v3 e3 a3; \
	v4 e4 a4; \
	v5 e5 a5; \
	v6 e6 a6; \
	v7 e7 a7; \
}

/* -- COMMON -- */

/* Syscall definition helpers */
#define KIP32_SYSCALL0(name) KIP32_SYSCALL_CORE(name,,,,,,,,,,,,,,,,);
#define KIP32_SYSCALL1(name, c0) KIP32_SYSCALL_CORE(name, =, c0,,,,,,,,,,,,,,);
#define KIP32_SYSCALL2(name, c0, c1) KIP32_SYSCALL_CORE(name, =, c0, =, c1,,,,,,,,,,,,);
#define KIP32_SYSCALL3(name, c0, c1, c2) KIP32_SYSCALL_CORE(name, =, c0, =, c1, =, c2,,,,,,,,,,);
#define KIP32_SYSCALL4(name, c0, c1, c2, c3) KIP32_SYSCALL_CORE(name, =, c0, =, c1, =, c2, =, c3,,,,,,,,);
#define KIP32_SYSCALL5(name, c0, c1, c2, c3, c4) KIP32_SYSCALL_CORE(name, =, c0, =, c1, =, c2, =, c3, =, c4,,,,,,);
#define KIP32_SYSCALL6(name, c0, c1, c2, c3, c4, c5) KIP32_SYSCALL_CORE(name, =, c0, =, c1, =, c2, =, c3, =, c4, =, c5,,,,);
#define KIP32_SYSCALL7(name, c0, c1, c2, c3, c4, c5, c6) KIP32_SYSCALL_CORE(name, =, c0, =, c1, =, c2, =, c3, =, c4, =, c5, =, c6,,);
#define KIP32_SYSCALL8(name, c0, c1, c2, c3, c4, c5, c6, c7) KIP32_SYSCALL_CORE(name, =, c0, =, c1, =, c2, =, c3, =, c4, =, c5, =, c6, =, c7);

#define KIP32_UDON_EXTERN(ext) KIP32_SYSCALL0("builtin_extern_" ext)
#define KIP32_UDON_PUSH(ku2) KIP32_SYSCALL0("builtin_push_" ext)
