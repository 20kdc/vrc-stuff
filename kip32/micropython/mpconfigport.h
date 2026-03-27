#include <stdint.h>
#include <alloca.h>

#define MICROPY_CONFIG_ROM_LEVEL                (MICROPY_CONFIG_ROM_LEVEL_FULL_FEATURES)
#define MICROPY_ENABLE_COMPILER                 (1)
#define MICROPY_ERROR_REPORTING                 (MICROPY_ERROR_REPORTING_DETAILED)

// necessary for time etc.
#define MICROPY_LONGINT_IMPL (MICROPY_LONGINT_IMPL_LONGLONG)

#define MICROPY_PY_GC (1)
#define MICROPY_ENABLE_GC (1)
#define MICROPY_MODULE_FROZEN (1)
#define MICROPY_MODULE_FROZEN_MPY (1)
#define MICROPY_HELPER_REPL (1)
#define MICROPY_REPL_EVENT_DRIVEN (0)
#define MICROPY_ENABLE_SCHEDULER (1)
#define MICROPY_KBD_EXCEPTION (1)

#define MICROPY_PY_IO (1)
#define MICROPY_PY_UCTYPES (1)

#define MICROPY_PY_SYS_STDFILES           (0)
#define MICROPY_PY_SYS_EXIT               (0)
#define MICROPY_PY_SYS_PATH               (0)
#define MICROPY_PY_SYS_ARGV               (0)

#define MICROPY_PY_TIME_TIME_TIME_NS (1)

typedef long mp_off_t;

#define MICROPY_HW_BOARD_NAME "magpie-alpha2"
#define MICROPY_HW_MCU_NAME "kip32"

#define MP_STATE_PORT MP_STATE_VM

extern void raisebat_periodic_timer();

#define MICROPY_VM_HOOK_LOOP { raisebat_periodic_timer(); }
#define MICROPY_VM_HOOK_RETURN { raisebat_periodic_timer(); }

#define KIP32_PYSTACK_SIZE 1024

extern const struct _mp_obj_type_t mp_udongimmicks_type_Coffee;

#define MICROPY_PORT_BUILTINS \
	{ MP_ROM_QSTR(MP_QSTR_Coffee), MP_ROM_PTR(&mp_udongimmicks_type_Coffee) },
