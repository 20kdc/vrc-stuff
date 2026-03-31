/* KIP-32 Udon header */

#pragma once

#ifndef KIP32_EXPORT
#include <kip32.h>
#endif

// Declares global assembly.
#define KIP32_UDON_GLOBALASM(id, code) KIP32_METADATA(id, "udon_asm:" code)
// Declares a specific assembly thing.
#define KIP32_UDON_ASM0(code) KIP32_SYSCALL0("builtin_asm:" code)
#define KIP32_UDON_ASM1(code, a0) KIP32_SYSCALL1("builtin_asm:" code, a0)
#define KIP32_UDON_ASM2(code, a0, a1) KIP32_SYSCALL2("builtin_asm:" code, a0, a1)
#define KIP32_UDON_ASM3(code, a0, a1, a2) KIP32_SYSCALL3("builtin_asm:" code, a0, a1, a2)
#define KIP32_UDON_ASM4(code, a0, a1, a2, a3) KIP32_SYSCALL4("builtin_asm:" code, a0, a1, a2, a3)
#define KIP32_UDON_ASM5(code, a0, a1, a2, a3, a4) KIP32_SYSCALL5("builtin_asm:" code, a0, a1, a2, a3, a4)
#define KIP32_UDON_ASM6(code, a0, a1, a2, a3, a4, a5) KIP32_SYSCALL6("builtin_asm:" code, a0, a1, a2, a3, a4, a5)
#define KIP32_UDON_ASM7(code, a0, a1, a2, a3, a4, a5, a6) KIP32_SYSCALL7("builtin_asm:" code, a0, a1, a2, a3, a4, a5, a6)
#define KIP32_UDON_ASM8(code, a0, a1, a2, a3, a4, a5, a6, a7) KIP32_SYSCALL8("builtin_asm:" code, a0, a1, a2, a3, a4, a5, a6, a7)

#define KIP32_UDON_EXTPFX "builtin_extern_"
#define KIP32_UDON_EXTERN0(ext) KIP32_SYSCALL0(KIP32_UDON_EXTPFX ext)
#define KIP32_UDON_PUSH(ku2) KIP32_SYSCALL0("builtin_push_" ku2)

// DO NOT STORE THIS STRUCT IN VARIABLES.
// It's a marker to say 'hey, this expression pushed onto the Udon Stack'
typedef struct {} kip32_udon_pushed_t;
#define KIP32_UDON_RETURN_PUSHED { kip32_udon_pushed_t ret = {}; return ret; }

#define KIP32_UDON_DECL_VAR(name, ku2) \
KIP32_INLINE(kip32_udon_pushed_t) kip32_udon_push_ ## name() { \
	KIP32_SYSCALL0("builtin_push_" ku2) \
	KIP32_UDON_RETURN_PUSHED \
}

#define KIP32_UDON_EXTERN1(ext, a0) { \
	(kip32_udon_pushed_t) (a0); \
	KIP32_SYSCALL0("builtin_extern_" ext); \
}
#define KIP32_UDON_EXTERN2(ext, a0, a1) { \
	(kip32_udon_pushed_t) (a0); \
	(kip32_udon_pushed_t) (a1); \
	KIP32_SYSCALL0("builtin_extern_" ext); \
}
#define KIP32_UDON_EXTERN3(ext, a0, a1, a2) { \
	(kip32_udon_pushed_t) (a0); \
	(kip32_udon_pushed_t) (a1); \
	(kip32_udon_pushed_t) (a2); \
	KIP32_SYSCALL0("builtin_extern_" ext); \
}
#define KIP32_UDON_EXTERN4(ext, a0, a1, a2, a3) { \
	(kip32_udon_pushed_t) (a0); \
	(kip32_udon_pushed_t) (a1); \
	(kip32_udon_pushed_t) (a2); \
	(kip32_udon_pushed_t) (a3); \
	KIP32_SYSCALL0("builtin_extern_" ext); \
}

#define KIP32_UDON_COPY() KIP32_SYSCALL0("stdsyscall_copy")
#define KIP32_UDON_COPY_S(src, dst) ( \
	KIP32_UDON_PUSH(src); \
	KIP32_UDON_PUSH(dst); \
	KIP32_UDON_COPY(); \
)

// -- Pushers --

KIP32_UDON_DECL_VAR(a0, "a0");
KIP32_UDON_DECL_VAR(a1, "a1");
KIP32_UDON_DECL_VAR(a2, "a2");
KIP32_UDON_DECL_VAR(a3, "a3");
KIP32_UDON_DECL_VAR(a4, "a4");
KIP32_UDON_DECL_VAR(a5, "a5");
KIP32_UDON_DECL_VAR(a6, "a6");
KIP32_UDON_DECL_VAR(a7, "a7");

KIP32_INLINE(kip32_udon_pushed_t) kip32_udon_push_stdsyscall_scratch_reg() {
	KIP32_SYSCALL0("stdsyscall_pushscratch");
	KIP32_UDON_RETURN_PUSHED;
}

// -- Proxies --

KIP32_DEF_SYSCALL0_RET(kip32_udon_bool_sense_internal, "stdsyscall_bool_sense")
KIP32_DEF_SYSCALL0_RET(kip32_udon_is_valid_internal, "stdsyscall_is_valid")
KIP32_DEF_SYSCALL0_RET(kip32_udon_i32_sense_internal, "stdsyscall_copy")

KIP32_DEF_SYSCALL0_RET(kip32_udon_str_len_internal, KIP32_UDON_EXTPFX "SystemString.__get_Length__SystemInt32")

KIP32_INLINE(void) kip32_udon_str_sub1_internal(int s) {
	KIP32_SYSCALL1(KIP32_UDON_EXTPFX "SystemString.__Substring__SystemInt32__SystemString", s);
}

KIP32_INLINE(void) kip32_udon_str_sub2_internal(int s, int l) {
	KIP32_SYSCALL2(KIP32_UDON_EXTPFX "SystemString.__Substring__SystemInt32_SystemInt32__SystemString", s, l);
}

KIP32_DEF_SYSCALL0_RET(kip32_udon_array_len_internal, KIP32_UDON_EXTPFX "SystemArray.__get_Length__SystemInt32")

KIP32_INLINE(void) kip32_udon_objarray_get_internal(int i) {
	KIP32_SYSCALL1(KIP32_UDON_EXTPFX "SystemObjectArray.__Get__SystemInt32__SystemObject", i);
}

KIP32_DEF_SYSCALL0_RET(kip32_udon_system_convert_i32_internal, KIP32_UDON_EXTPFX "SystemConvert.__ToInt32__SystemObject__SystemInt32")

KIP32_INLINE(void) kip32_udon_system_convert_char_internal(int i) {
	KIP32_SYSCALL1(KIP32_UDON_EXTPFX "SystemConvert.__ToChar__SystemInt32__SystemChar", i);
}

KIP32_INLINE(void) kip32_udon_chararray_get_internal(int i) {
	KIP32_SYSCALL1(KIP32_UDON_EXTPFX "SystemCharArray.__Get__SystemInt32__SystemChar", i);
}

// -- Direct Values --

#define KIP32_UDON_BOOL_SENSE(p) ((kip32_udon_pushed_t) (p), kip32_udon_bool_sense_internal())

#define KIP32_UDON_IS_VALID(p) ((kip32_udon_pushed_t) (p), kip32_udon_is_valid_internal())

#define KIP32_UDON_I32_SENSE(p) ((kip32_udon_pushed_t) (p), kip32_udon_i32_sense_internal())

#define KIP32_UDON_I32_PUT(p, v) { \
	kip32_udon_push_a0(); \
	(kip32_udon_pushed_t) (p); \
	KIP32_SYSCALL1("stdsyscall_copy", v); \
}

// -- Debug --

#define KIP32_UDON_DEBUG_LOG(p) KIP32_UDON_EXTERN1("UnityEngineDebug.__Log__SystemObject__SystemVoid", p)

// -- Strings --

#define KIP32_UDON_STR_LEN(p) ((kip32_udon_pushed_t) (p), kip32_udon_push_a0(), kip32_udon_str_len_internal())

#define KIP32_UDON_STR_SUB1(p, s, r) { \
	(kip32_udon_pushed_t) (p); \
	kip32_udon_push_a0(); \
	(kip32_udon_pushed_t) (r); \
	kip32_udon_str_sub1_internal(s); \
}

#define KIP32_UDON_STR_SUB2(p, s, l, r) { \
	(kip32_udon_pushed_t) (p); \
	kip32_udon_push_a0(); \
	kip32_udon_push_a1(); \
	(kip32_udon_pushed_t) (r); \
	kip32_udon_str_sub2_internal(s, l); \
}

#define KIP32_UDON_STR_TOCHARARRAY(p, r) KIP32_UDON_EXTERN2("SystemString.__ToCharArray__SystemCharArray", p, r)

#define KIP32_UDON_UCS2_CHR(v, r) { \
	kip32_udon_push_a0(); \
	(kip32_udon_pushed_t) (r); \
	kip32_udon_system_convert_char_internal(v); \
}

#define KIP32_UDON_UCS2_STR(v, r) { \
	kip32_udon_push_a0(); \
	kip32_udon_push_stdsyscall_scratch_reg(); \
	kip32_udon_system_convert_char_internal(v); \
	KIP32_UDON_EXTERN2("SystemConvert.__ToString__SystemChar__SystemString", kip32_udon_push_stdsyscall_scratch_reg(), r); \
}

// -- Arrays --

#define KIP32_UDON_ARRAY_LEN(p) ((kip32_udon_pushed_t) (p), kip32_udon_push_a0(), kip32_udon_array_len_internal())

#define KIP32_UDON_OBJARRAY_GET(p, i, r) { \
	(kip32_udon_pushed_t) (p); \
	kip32_udon_push_a0(); \
	(kip32_udon_pushed_t) (r); \
	kip32_udon_objarray_get_internal(i); \
}

#define KIP32_UDON_CHARARRAY_GET(p, i) ( \
	(kip32_udon_pushed_t) (p), \
	kip32_udon_push_a0(), \
	kip32_udon_push_stdsyscall_scratch_reg(), \
	kip32_udon_chararray_get_internal(i), \
	kip32_udon_push_stdsyscall_scratch_reg(), \
	kip32_udon_push_a0(), \
	kip32_udon_system_convert_i32_internal() \
)
