#include <string.h>
#include <kip32.h>
#include <kip32_udon.h>

#include "py/builtin.h"
#include "py/compile.h"
#include "py/runtime.h"
#include "py/repl.h"
#include "py/gc.h"
#include "py/mperrno.h"
#include "shared/runtime/pyexec.h"

#define BAT_HEAP_WORDS 0x10000

// 256k heap
int micropython_heap[0x10000];

// stack figuring-out
int fresh_reset = 1;
void * stack_top;

// where allocated stack RAM is 'likely' to begin
extern char _ebss[];

// enlightenments

static void debug_puts(const char * str) {
	while (*str) {
		int v = *str++;
		KIP32_SYSCALL1("stdsyscall_putchar", v);
	}
}

void * memmove(void * dest, const void * src, size_t n) {
	intptr_t dest_i = (intptr_t) dest;
	intptr_t src_i = (intptr_t) src;
	intptr_t n_i = (intptr_t) n;
	KIP32_SYSCALL3("stdsyscall_memmove", dest_i, src_i, n_i);
	return dest;
}

void * memcpy(void * dest, const void * src, size_t n) {
	return memmove(dest, src, n);
}

KIP32_UDON_DECL_VAR(raisebat_line_in, "raisebat_line_in");
KIP32_UDON_DECL_VAR(raisebat_temp0, "raisebat_temp0");
KIP32_UDON_DECL_VAR(raisebat_temp1, "raisebat_temp1");
KIP32_UDON_DECL_VAR(raisebat_target, "raisebat_target");
KIP32_UDON_DECL_VAR(raisebat_empty_string, "C(string(\"\"))");
KIP32_UDON_DECL_VAR(raisebat_cpu_char_string, "C(string(\"cpu_char\"))");
KIP32_UDON_DECL_VAR(raisebat__cpu_char_string, "C(string(\"_cpu_char\"))");
KIP32_UDON_DECL_VAR(raisebat_ub_type, "C(type(\"VRCUdonCommonInterfacesIUdonEventReceiver\"))");

// conout driver core

void raisebat_conout_putchar(int v) {
	KIP32_UDON_UCS2_CHR(v & 0xFF, kip32_udon_push_raisebat_temp0());
	KIP32_UDON_EXTERN3("UnityEngineTransform.__GetComponent__SystemType__UnityEngineComponent", kip32_udon_push_raisebat_target(), kip32_udon_push_raisebat_ub_type(), kip32_udon_push_raisebat_temp1());
	KIP32_UDON_EXTERN3("VRCUdonCommonInterfacesIUdonEventReceiver.__SetProgramVariable__SystemString_SystemObject__SystemVoid", kip32_udon_push_raisebat_temp1(), kip32_udon_push_raisebat_cpu_char_string(), kip32_udon_push_raisebat_temp0());
	KIP32_UDON_EXTERN2("VRCUdonCommonInterfacesIUdonEventReceiver.__SendCustomEvent__SystemString__SystemVoid", kip32_udon_push_raisebat_temp1(), kip32_udon_push_raisebat__cpu_char_string());
}

// main loop

void KIP32_EXPORT _start() {
	if (fresh_reset) {
		fresh_reset = 0;
		debug_puts("MicroPython: init - we're doing update_yield test now\n");
		KIP32_SYSCALL0("stdsyscall_update_yield");
		debug_puts("MicroPython: we survived 1st yield. initializing GC/MP\n");

		// this is a pretty damn nasty trick, but it catches autostack
		stack_top = kip32_udon_sbrk(0);

#if MICROPY_ENABLE_PYSTACK
		static mp_obj_t pystack[KIP32_PYSTACK_SIZE];
		mp_pystack_init(pystack, &pystack[KIP32_PYSTACK_SIZE]);
#endif

		mp_cstack_init_with_top(stack_top, (mp_uint_t) stack_top - (mp_uint_t) _ebss);

		gc_init(micropython_heap, micropython_heap + BAT_HEAP_WORDS);
		mp_init();

		// add Coffee() class

		debug_puts("MicroPython: we survived MP init, entering REPL\n");
		while (1) {
			KIP32_SYSCALL0("stdsyscall_update_yield");
			debug_puts("MicroPython: REPL restart\n");
			pyexec_friendly_repl();
		}
	}
}

// handlers

mp_uint_t mp_hal_ticks_ms() {
	// would be nice to do something with SystemDateTime here
	// we don't have the time, though
	return 0;
}

void KIP32_EXPORT raisebat_break() {
	mp_sched_keyboard_interrupt();
}

// see MICROPY_VM_HOOK_LOOP
void raisebat_periodic_timer() {
	static int raisebat_yield_periodic = 0;
	raisebat_yield_periodic++;
	if (raisebat_yield_periodic >= 256) {
		KIP32_SYSCALL0("stdsyscall_update_yield");
		raisebat_yield_periodic = 0;
	}
}

mp_lexer_t *mp_lexer_new_from_file(qstr filename) {
	mp_raise_OSError(MP_ENOENT);
}

mp_import_stat_t mp_import_stat(const char *path) {
	return MP_IMPORT_STAT_NO_EXIST;
}


void nlr_jump_fail(void * val) {
	KIP32_SYSCALL0("builtin_abort");
	while (1) {
		// this is really just to stop GCC complaining, as the syscall will prevent return quite nicely
		KIP32_SYSCALL0("stdsyscall_update_yield");
	}
}

int mp_hal_stdin_rx_chr() {
	// string coming in from raisebat_line_in
	while (1) {
		if (KIP32_UDON_IS_VALID(kip32_udon_push_raisebat_line_in()))
			if (KIP32_UDON_STR_LEN(kip32_udon_push_raisebat_line_in()))
				break;
		KIP32_SYSCALL0("stdsyscall_update_yield");
	}
	// doing this per-char is theoretically very bad but what can 'ya do
	KIP32_UDON_STR_TOCHARARRAY(kip32_udon_push_raisebat_line_in(), kip32_udon_push_raisebat_temp0());
	KIP32_UDON_STR_SUB1(kip32_udon_push_raisebat_line_in(), 1, kip32_udon_push_raisebat_line_in());
	int c = KIP32_UDON_CHARARRAY_GET(kip32_udon_push_raisebat_temp0(), 0);
	if (c >= 0x100)
		c = 0xFF;
	return c;
}

void mp_hal_stdout_tx_strn_cooked(const char *str, size_t len) {
	while (len--) {
		raisebat_conout_putchar(*str++);
	}
}

void mp_hal_stdout_tx_strn(const char *str, size_t len) {
	mp_hal_stdout_tx_strn_cooked(str, len);
}

void mp_hal_stdout_tx_str(const char *str) {
	mp_hal_stdout_tx_strn(str, strlen(str));
}

void gc_collect(void) {
	// force spill
	register intptr_t force_stack_s0 __asm__("s0");
	register intptr_t force_stack_s1 __asm__("s1");
	register intptr_t force_stack_s2 __asm__("s2");
	register intptr_t force_stack_s3 __asm__("s3");
	register intptr_t force_stack_s4 __asm__("s4");
	register intptr_t force_stack_s5 __asm__("s5");
	register intptr_t force_stack_s6 __asm__("s6");
	register intptr_t force_stack_s7 __asm__("s7");
	register intptr_t force_stack_s8 __asm__("s8");
	register intptr_t force_stack_s9 __asm__("s9");
	register intptr_t force_stack_s10 __asm__("s10");
	register intptr_t force_stack_s11 __asm__("s11");
	// this NOP helps confirm where these are in the instruction stream so we KNOW it was definitely setup
	__asm__ volatile (
		"NOP"
		: "=r" (force_stack_s0), "=r" (force_stack_s1), "=r" (force_stack_s2), "=r" (force_stack_s3), "=r" (force_stack_s4), "=r" (force_stack_s5)
		, "=r" (force_stack_s6), "=r" (force_stack_s7), "=r" (force_stack_s8), "=r" (force_stack_s9), "=r" (force_stack_s10), "=r" (force_stack_s11)
		: "r" (force_stack_s0), "r" (force_stack_s1), "r" (force_stack_s2), "r" (force_stack_s3), "r" (force_stack_s4), "r" (force_stack_s5)
		, "r" (force_stack_s6), "r" (force_stack_s7), "r" (force_stack_s8), "r" (force_stack_s9), "r" (force_stack_s10), "r" (force_stack_s11)
		:
	);
	intptr_t force_stack[] = {
		force_stack_s0,
		force_stack_s1,
		force_stack_s2,
		force_stack_s3,
		force_stack_s4,
		force_stack_s5,
		force_stack_s6,
		force_stack_s7,
		force_stack_s8,
		force_stack_s9,
		force_stack_s10,
		force_stack_s11,
	};
	gc_collect_start();
	gc_collect_root((void *) force_stack, ((mp_uint_t) stack_top - (mp_uint_t) force_stack) / sizeof(mp_uint_t));
	gc_collect_end();
	gc_dump_info(&mp_plat_print);
}

#ifndef NDEBUG
// this is baaaaad
void MP_WEAK __assert_func(const char *file, int line, const char *func, const char *expr) {
	mp_hal_stdout_tx_strn_cooked("ASSERT ", 7);
	mp_hal_stdout_tx_strn_cooked(file, strlen(file));
	mp_hal_stdout_tx_strn_cooked(" ", 1);
	mp_hal_stdout_tx_strn_cooked(func, strlen(func));
	mp_hal_stdout_tx_strn_cooked(" ", 1);
	mp_hal_stdout_tx_strn_cooked(expr, strlen(expr));
	KIP32_SYSCALL1("stdsyscall_putchar", '\n');
	KIP32_SYSCALL0("builtin_abort");
}
#endif
