include ../../py/mkenv.mk

include $(TOP)/py/py.mk
# this is where you WOULD enable this if you wanted it, but licensing is complicated.
# plus, zicsr moment
# include $(TOP)/extmod/extmod.mk

MICROPY_ROM_TEXT_COMPRESSION ?= 0

CROSS_COMPILE ?= riscv64-unknown-elf-

CFLAGS += -I. -I$(TOP) -I$(BUILD)

ifeq ($(KIP32_LIBC), 1)
CFLAGS += -mabi=ilp32 -march=rv32im -DMICROPY_USE_INTERNAL_ERRNO=1 -specs=$(KIP32_SDK)/kip32_udon.specs
else
CFLAGS += -mabi=ilp32 -march=rv32im -I$(KIP32_SDK)/include -specs=/usr/lib/picolibc/riscv64-unknown-elf/picolibc.specs
endif

CFLAGS += -Wall -Werror -std=c99
CSUPEROPT = -Os

ifeq ($(DEBUG), 1)
CFLAGS += -Og
else
CFLAGS += -Os -DNDEBUG
CFLAGS += -fdata-sections -ffunction-sections
endif

CFLAGS += -nostartfiles -T $(KIP32_SDK)/kip32.ld

SRC_C += main.c \
	modgimmicks.c \
	shared/readline/readline.c \
	shared/runtime/pyexec.c \
	$(BUILD)/_frozen_mpy.c

SRC_QSTR += modgimmicks.c \
	shared/readline/readline.c \
	shared/runtime/pyexec.c

OBJ += $(PY_O)
OBJ += $(addprefix $(BUILD)/, $(SRC_C:.c=.o))

all: $(BUILD)/firmware.udonjson $(BUILD)/firmware.uasm

$(BUILD)/_frozen_mpy.c: $(TOP)/tests/frozen/frozentest.mpy $(BUILD)/genhdr/qstrdefs.generated.h
	$(ECHO) "MISC freezing bytecode"
	$(Q)$(TOP)/tools/mpy-tool.py -f -q $(BUILD)/genhdr/qstrdefs.preprocessed.h -mlongint-impl=none $< > $@

$(BUILD)/firmware.udonjson: $(BUILD)/firmware.elf terminal.ron $(KIP32_SDK)/stdsyscall.ron
	$(KIP32_SDK)/elf2uasm -o $@ $(BUILD)/firmware.elf --udonjson --inc terminal.ron
$(BUILD)/firmware.uasm: $(BUILD)/firmware.elf terminal.ron $(KIP32_SDK)/stdsyscall.ron
	$(KIP32_SDK)/elf2uasm -o $@ $(BUILD)/firmware.elf --ignore-emit-err --inc terminal.ron

$(BUILD)/firmware.elf: $(OBJ)
	$(ECHO) "LINK $@"
	$(Q)$(CC) $(CFLAGS) -o $@ $^
	$(Q)$(SIZE) $@

include $(TOP)/py/mkrules.mk
