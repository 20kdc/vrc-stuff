/* KIP-32 SDK header */

#pragma once

#include <stdint.h>

/* Exported symbols are put into a specific section. */
#define KIP32_EXPORT __attribute__((section(".kip32_export")))

/* We're subtly trying to tell the compiler to remove these memory accesses. */
#define KIP32_SYSCALL_CORE(name, e0, v0, e1, v1, e2, v2, e3, v3, e4, v4, e5, v5, e6, v6, e7, v7) \
{ \
	register intptr_t KIP32_SYSCALL_a0 __asm__("a0") e0 v0; \
	register intptr_t KIP32_SYSCALL_a1 __asm__("a1") e1 v1; \
	register intptr_t KIP32_SYSCALL_a2 __asm__("a2") e2 v2; \
	register intptr_t KIP32_SYSCALL_a3 __asm__("a3") e3 v3; \
	register intptr_t KIP32_SYSCALL_a4 __asm__("a4") e4 v4; \
	register intptr_t KIP32_SYSCALL_a5 __asm__("a5") e5 v5; \
	register intptr_t KIP32_SYSCALL_a6 __asm__("a6") e6 v6; \
	register intptr_t KIP32_SYSCALL_a7 __asm__("a7") e7 v7; \
	register intptr_t KIP32_SYSCALL_ra __asm__("ra"); \
	register intptr_t KIP32_SYSCALL_t0 __asm__("t0"); \
	register intptr_t KIP32_SYSCALL_t1 __asm__("t1"); \
	register intptr_t KIP32_SYSCALL_t2 __asm__("t2"); \
	register intptr_t KIP32_SYSCALL_t3 __asm__("t3"); \
	register intptr_t KIP32_SYSCALL_t4 __asm__("t4"); \
	register intptr_t KIP32_SYSCALL_t5 __asm__("t5"); \
	register intptr_t KIP32_SYSCALL_t6 __asm__("t6"); \
	static const char __attribute__((section(".kip32_metadata"))) syscallname[] = "syscall:" name; \
	/* A more direct method of expression-calls was chosen, but the compiler tries to be clever and obfuscates JALs in the process. */ \
	__asm__ volatile ( \
		"jal %8\n" \
		: "=r" (KIP32_SYSCALL_a0), "=r" (KIP32_SYSCALL_a1), "=r" (KIP32_SYSCALL_a2), "=r" (KIP32_SYSCALL_a3), "=r" (KIP32_SYSCALL_a4), "=r" (KIP32_SYSCALL_a5), "=r" (KIP32_SYSCALL_a6), "=r" (KIP32_SYSCALL_a7) \
		: "s" (syscallname), "r" (KIP32_SYSCALL_a0), "r" (KIP32_SYSCALL_a1), "r" (KIP32_SYSCALL_a2), "r" (KIP32_SYSCALL_a3), "r" (KIP32_SYSCALL_a4), "r" (KIP32_SYSCALL_a5), "r" (KIP32_SYSCALL_a6), "r" (KIP32_SYSCALL_a7), \
		  "r" (KIP32_SYSCALL_ra), "r" (KIP32_SYSCALL_t0), "r" (KIP32_SYSCALL_t1), "r" (KIP32_SYSCALL_t2), "r" (KIP32_SYSCALL_t3), "r" (KIP32_SYSCALL_t4), "r" (KIP32_SYSCALL_t5), "r" (KIP32_SYSCALL_t6)  \
		: \
	); \
	(void) (v0 e0 KIP32_SYSCALL_a0); \
	(void) (v1 e1 KIP32_SYSCALL_a1); \
	(void) (v2 e2 KIP32_SYSCALL_a2); \
	(void) (v3 e3 KIP32_SYSCALL_a3); \
	(void) (v4 e4 KIP32_SYSCALL_a4); \
	(void) (v5 e5 KIP32_SYSCALL_a5); \
	(void) (v6 e6 KIP32_SYSCALL_a6); \
	(void) (v7 e7 KIP32_SYSCALL_a7); \
}

/* -- COMMON -- */

#define KIP32_INLINE(ty) static inline ty __attribute__((always_inline))

/* Syscall definition helpers */
#define KIP32_SYSCALL0(name) KIP32_SYSCALL_CORE(name,,,,,,,,,,,,,,,,)
#define KIP32_SYSCALL1(name, c0) KIP32_SYSCALL_CORE(name, =, c0,,,,,,,,,,,,,,)
#define KIP32_SYSCALL2(name, c0, c1) KIP32_SYSCALL_CORE(name, =, c0, =, c1,,,,,,,,,,,,)
#define KIP32_SYSCALL3(name, c0, c1, c2) KIP32_SYSCALL_CORE(name, =, c0, =, c1, =, c2,,,,,,,,,,)
#define KIP32_SYSCALL4(name, c0, c1, c2, c3) KIP32_SYSCALL_CORE(name, =, c0, =, c1, =, c2, =, c3,,,,,,,,)
#define KIP32_SYSCALL5(name, c0, c1, c2, c3, c4) KIP32_SYSCALL_CORE(name, =, c0, =, c1, =, c2, =, c3, =, c4,,,,,,)
#define KIP32_SYSCALL6(name, c0, c1, c2, c3, c4, c5) KIP32_SYSCALL_CORE(name, =, c0, =, c1, =, c2, =, c3, =, c4, =, c5,,,,)
#define KIP32_SYSCALL7(name, c0, c1, c2, c3, c4, c5, c6) KIP32_SYSCALL_CORE(name, =, c0, =, c1, =, c2, =, c3, =, c4, =, c5, =, c6,,)
#define KIP32_SYSCALL8(name, c0, c1, c2, c3, c4, c5, c6, c7) KIP32_SYSCALL_CORE(name, =, c0, =, c1, =, c2, =, c3, =, c4, =, c5, =, c6, =, c7)

#define KIP32_DEF_SYSCALL0_RET(name, syscall) \
KIP32_INLINE(intptr_t) name() { \
	intptr_t ret = 0; \
	KIP32_SYSCALL1(syscall, ret) \
	return ret; \
}
