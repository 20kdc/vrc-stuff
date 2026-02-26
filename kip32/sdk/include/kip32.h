/* KIP-32 SDK header */

#pragma once

#include <stdint.h>

/* Exported symbols are put into a specific section. */
#define KIP32_EXPORT __attribute__((section(".kip32_export")))

/* We're subtly trying to tell the compiler to remove these memory accesses. */
#define KIP32_SYSCALL_CORE(name, cstr, e0, v0, e1, v1, e2, v2, e3, v3, e4, v4, e5, v5, e6, v6, e7, v7) \
static inline __attribute__((always_inline)) void name { \
	register intptr_t a0 __asm__("a0") e0 v0; \
	register intptr_t a1 __asm__("a1") e1 v1; \
	register intptr_t a2 __asm__("a2") e2 v2; \
	register intptr_t a3 __asm__("a3") e3 v3; \
	register intptr_t a4 __asm__("a4") e4 v4; \
	register intptr_t a5 __asm__("a5") e5 v5; \
	register intptr_t a6 __asm__("a6") e6 v6; \
	register intptr_t a7 __asm__("a7") e7 v7; \
	static const char __attribute__((section(".kip32_zero_metadata"))) syscallname[] = cstr; \
	/* This is to write EBREAK followed by the address of a C-string syscall name. */ \
	/* The new converter identifies this and reads the string. */ \
	__asm__ volatile ( \
		"ebreak\n" \
		".word %8" \
		: "=r" (a0), "=r" (a1), "=r" (a2), "=r" (a3), "=r" (a4), "=r" (a5), "=r" (a6), "=r" (a7) \
		: "s" (syscallname), "r" (a0), "r" (a1), "r" (a2), "r" (a3), "r" (a4), "r" (a5), "r" (a6), "r" (a7) \
		: \
	); \
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
#define KIP32_SYSCALL0(name, cstr) KIP32_SYSCALL_CORE(name(),cstr,,,,,,,,,,,,,,,,);
#define KIP32_SYSCALL1(name, cstr) KIP32_SYSCALL_CORE(name(intptr_t * c0),cstr, =, *c0,,,,,,,,,,,,,,);
#define KIP32_SYSCALL2(name, cstr) KIP32_SYSCALL_CORE(name(intptr_t * c0, intptr_t * c1),cstr, =, *c0, =, *c1,,,,,,,,,,,,);
#define KIP32_SYSCALL3(name, cstr) KIP32_SYSCALL_CORE(name(intptr_t * c0, intptr_t * c1, intptr_t * c2),cstr, =, *c0, =, *c1, =, *c2,,,,,,,,,,);
#define KIP32_SYSCALL4(name, cstr) KIP32_SYSCALL_CORE(name(intptr_t * c0, intptr_t * c1, intptr_t * c2, intptr_t * c3),cstr, =, *c0, =, *c1, =, *c2, =, *c3,,,,,,,,);
#define KIP32_SYSCALL5(name, cstr) KIP32_SYSCALL_CORE(name(intptr_t * c0, intptr_t * c1, intptr_t * c2, intptr_t * c3, intptr_t * c4),cstr, =, *c0, =, *c1, =, *c2, =, *c3, =, *c4,,,,,,);
#define KIP32_SYSCALL6(name, cstr) KIP32_SYSCALL_CORE(name(intptr_t * c0, intptr_t * c1, intptr_t * c2, intptr_t * c3, intptr_t * c4, intptr_t * c5),cstr, =, *c0, =, *c1, =, *c2, =, *c3, =, *c4, =, *c5,,,,);
#define KIP32_SYSCALL7(name, cstr) KIP32_SYSCALL_CORE(name(intptr_t * c0, intptr_t * c1, intptr_t * c2, intptr_t * c3, intptr_t * c4, intptr_t * c5, intptr_t * c6),cstr, =, *c0, =, *c1, =, *c2, =, *c3, =, *c4, =, *c5, =, *c6,,);
#define KIP32_SYSCALL8(name, cstr) KIP32_SYSCALL_CORE(name(intptr_t * c0, intptr_t * c1, intptr_t * c2, intptr_t * c3, intptr_t * c4, intptr_t * c5, intptr_t * c6, intptr_t * c7),cstr, =, *c0, =, *c1, =, *c2, =, *c3, =, *c4, =, *c5, =, *c6, =, *c7);
