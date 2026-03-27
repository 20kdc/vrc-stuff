#include <string.h>
#include <setjmp.h>
#include <stdio.h>
#include <unistd.h>
#include <kip32.h>
#include <kip32_udon.h>
#include <time.h>

#include "py/builtin.h"
#include "py/compile.h"
#include "py/runtime.h"
#include "py/repl.h"
#include "py/gc.h"
#include "py/mperrno.h"
#include "shared/runtime/pyexec.h"

#define BAT_HEAP_WORDS 0x10000

// 256k heap
int micropython_heap[BAT_HEAP_WORDS];

// stack figuring-out
int fresh_reset = 1;
void * stack_top;

// where allocated stack RAM is 'likely' to begin
extern char _end[];

// enlightenments

FILE * stderr = &__kip32_libc_systemout;

#ifdef __KIP32_QEMU__
int system(const char * string) {
	write(2, string, strlen(string));
	return -1;
}
#endif

// 'magpie' update yield

static jmp_buf magpie_update_yield_buf;
static int magpie_update_yield_valid = 0;
static uint64_t magpie_cpuclock;
static long long magpie_cpuclock_base;
static long long magpie_ticks_base;

void KIP32_EXPORT _update() {
	if (magpie_update_yield_valid)
		longjmp(magpie_update_yield_buf, 1);
}

void magpie_update_yield() {
	if (!setjmp(magpie_update_yield_buf)) {
		magpie_cpuclock += __kip32_gettimeus() - magpie_cpuclock_base;
		magpie_update_yield_valid = 1;
#ifndef KIP32_NO_SYSCALLS
		KIP32_SYSCALL0("builtin_abort");
#else
		_update();
#endif
	} else {
		magpie_cpuclock_base = __kip32_gettimeus();
		magpie_update_yield_valid = 0;
	}
}

// ...

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
#ifndef KIP32_NO_SYSCALLS
	KIP32_UDON_UCS2_CHR(v & 0xFF, kip32_udon_push_raisebat_temp0());
	KIP32_UDON_EXTERN3("UnityEngineTransform.__GetComponent__SystemType__UnityEngineComponent", kip32_udon_push_raisebat_target(), kip32_udon_push_raisebat_ub_type(), kip32_udon_push_raisebat_temp1());
	KIP32_UDON_EXTERN3("VRCUdonCommonInterfacesIUdonEventReceiver.__SetProgramVariable__SystemString_SystemObject__SystemVoid", kip32_udon_push_raisebat_temp1(), kip32_udon_push_raisebat_cpu_char_string(), kip32_udon_push_raisebat_temp0());
	KIP32_UDON_EXTERN2("VRCUdonCommonInterfacesIUdonEventReceiver.__SendCustomEvent__SystemString__SystemVoid", kip32_udon_push_raisebat_temp1(), kip32_udon_push_raisebat__cpu_char_string());
#else
	fputc(v, stderr);
#endif
}

// main loop

void KIP32_EXPORT _start() {
	if (fresh_reset) {
		fresh_reset = 0;
		magpie_ticks_base = magpie_cpuclock_base = __kip32_gettimeus();
		fputs("MicroPython: " MICROPY_HW_BOARD_NAME " " __DATE__ " " __TIME__ ": init - testing update_yield\n", stderr);
		magpie_update_yield();
		fputs("MicroPython: we survived 1st yield. initializing GC/MP\n", stderr);

#ifndef __KIP32_QEMU__
		// this is a pretty damn nasty trick, but it catches autostack
		stack_top = sbrk(0);
#else
		// this is an even nastier trick, mirroring mp_cstack_init_with_sp_here
		stack_top = alloca(4);
#endif
		// somehow this works on QEMU too soooo
		size_t stack_size = (size_t) stack_top - (size_t) _end;

		fprintf(stderr, "MicroPython: iSP = %p, 0x%08zx stack bytes available\n", stack_top, stack_size);

#if MICROPY_ENABLE_PYSTACK
		static mp_obj_t pystack[KIP32_PYSTACK_SIZE];
		mp_pystack_init(pystack, &pystack[KIP32_PYSTACK_SIZE]);
#endif

		fputs("MicroPython: pystack go\n", stderr);

		mp_cstack_init_with_top(stack_top, stack_size);

		fputs("MicroPython: cstack go\n", stderr);

		gc_init(micropython_heap, micropython_heap + BAT_HEAP_WORDS);

		fputs("MicroPython: GC go\n", stderr);

		mp_init();

		fputs("MicroPython: we survived MP init, entering REPL\n", stderr);
		while (1) {
			magpie_update_yield();
			fputs("MicroPython: REPL restart\n", stderr);
			pyexec_friendly_repl();
		}
	}
}

// time

mp_uint_t mp_hal_ticks_ms() {
	return (mp_uint_t) ((__kip32_gettimeus() - magpie_ticks_base) / 1000);
}

mp_uint_t mp_hal_ticks_us() {
	return (mp_uint_t) (__kip32_gettimeus() - magpie_ticks_base);
}

mp_uint_t mp_hal_ticks_cpu() {
	return (mp_uint_t) magpie_cpuclock;
}

uint64_t mp_hal_time_ns() {
	return (uint64_t) (__kip32_gettimeus() * 1000);
}

mp_obj_t mp_time_time_get() {
	return mp_obj_new_int_from_ll(__kip32_gettimeus() / 1000000LL);
}

static void mp_hal_delay_until(unsigned long long end) {
	while (((unsigned long long) __kip32_gettimeus()) < end) {
		mp_handle_pending(MP_HANDLE_PENDING_CALLBACKS_AND_EXCEPTIONS);
		magpie_update_yield();
	}
}

void mp_hal_delay_us(mp_uint_t us) {
	mp_hal_delay_until(__kip32_gettimeus() + us);
}

void mp_hal_delay_ms(mp_uint_t ms) {
	mp_hal_delay_until(__kip32_gettimeus() + (ms * 1000));
}

// handlers

void KIP32_EXPORT raisebat_break() {
	mp_sched_keyboard_interrupt();
}

// see MICROPY_VM_HOOK_LOOP
void raisebat_periodic_timer() {
	static int raisebat_yield_periodic = 0;
	raisebat_yield_periodic++;
	if (raisebat_yield_periodic >= 256) {
		magpie_update_yield();
		raisebat_yield_periodic = 0;
	}
}

mp_lexer_t *mp_lexer_new_from_file(qstr filename) {
	mp_raise_OSError(MP_ENOENT);
}

// 'VFS stuff' {
mp_import_stat_t mp_import_stat(const char *path) {
	return MP_IMPORT_STAT_NO_EXIST;
}

mp_obj_t mp_builtin_open(size_t n_args, const mp_obj_t *args, mp_map_t *kwargs) {
    return mp_const_none;
}
MP_DEFINE_CONST_FUN_OBJ_KW(mp_builtin_open_obj, 1, mp_builtin_open);
// }

void nlr_jump_fail(void * val) {
#ifdef __KIP32_QEMU__
	_exit(1);
#else
	KIP32_SYSCALL0("builtin_abort");
#endif
	while (1) {
		// this is really just to stop GCC complaining, as the syscall will prevent return quite nicely
		magpie_update_yield();
	}
}

int mp_hal_stdin_rx_chr() {
#ifdef __KIP32_QEMU__
	char c = 0;
	while (read(0, &c, 1) != 1);
	return c & 0xFF;
#else
	// string coming in from raisebat_line_in
	while (1) {
		if (KIP32_UDON_IS_VALID(kip32_udon_push_raisebat_line_in()))
			if (KIP32_UDON_STR_LEN(kip32_udon_push_raisebat_line_in()))
				break;
		mp_handle_pending(MP_HANDLE_PENDING_CALLBACKS_AND_EXCEPTIONS);
		magpie_update_yield();
	}
	// doing this per-char is theoretically very bad but what can 'ya do
	KIP32_UDON_STR_TOCHARARRAY(kip32_udon_push_raisebat_line_in(), kip32_udon_push_raisebat_temp0());
	KIP32_UDON_STR_SUB1(kip32_udon_push_raisebat_line_in(), 1, kip32_udon_push_raisebat_line_in());
	int c = KIP32_UDON_CHARARRAY_GET(kip32_udon_push_raisebat_temp0(), 0);
	if (c >= 0x100)
		c = 0xFF;
	return c;
#endif
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

void __assert_fail(const char *expr, const char *file, int line, const char *func) {
	mp_hal_stdout_tx_strn_cooked("ASSERT ", 7);
	mp_hal_stdout_tx_strn_cooked(file, strlen(file));
	mp_hal_stdout_tx_strn_cooked(" ", 1);
	mp_hal_stdout_tx_strn_cooked(func, strlen(func));
	mp_hal_stdout_tx_strn_cooked(" ", 1);
	mp_hal_stdout_tx_strn_cooked(expr, strlen(expr));
	mp_hal_stdout_tx_strn_cooked("\n", 1);
#ifdef __KIP32_QEMU__
	_exit(1);
#else
	KIP32_SYSCALL0("builtin_abort");
#endif
}

#ifndef NDEBUG
// this is baaaaad
void __assert_func(const char *file, int line, const char *func, const char *expr) {
	__assert_fail(expr, file, line, func);
}

#endif

int DEBUG_printf(const char * format, ...) {
	va_list args;
	va_start(args, format);
	int r = vfprintf(stderr, format, args);
	va_end(args);
	return r;
}
