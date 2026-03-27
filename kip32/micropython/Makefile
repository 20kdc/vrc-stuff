include ../../py/mkenv.mk

include $(TOP)/py/py.mk
# this is where you WOULD enable this if you wanted it, but licensing is complicated.
# plus, zicsr moment
# include $(TOP)/extmod/extmod.mk

MICROPY_ROM_TEXT_COMPRESSION ?= 0

CROSS_COMPILE ?= riscv64-unknown-elf-

CFLAGS += -I. -I$(TOP) -I$(BUILD)

ifeq ($(QEMU), 1)
CFLAGS += -mabi=ilp32 -march=rv32im -DMICROPY_USE_INTERNAL_ERRNO=1 -specs=$(KIP32_SDK)/libcqemu.specs
else
CFLAGS += -mabi=ilp32 -march=rv32im -DMICROPY_USE_INTERNAL_ERRNO=1 -specs=$(KIP32_SDK)/kip32_udon.specs
endif

CFLAGS += -Wl,-Map=$(BUILD)/firmware.map

CFLAGS += -Wall -Werror -std=c99
CSUPEROPT = -Os

ifeq ($(DEBUG), 1)
CFLAGS += -Og
else
CFLAGS += -Os
CFLAGS += -DNDEBUG
CFLAGS += -fdata-sections -ffunction-sections
endif

SRC_C_AND_QSTR += modgimmicks.c \
	shared/readline/readline.c \
	shared/runtime/pyexec.c \
	extmod/modjson.c \
	extmod/moductypes.c \
	extmod/modtime.c

SRC_C += main.c \
	$(SRC_C_AND_QSTR) \
	$(BUILD)/_frozen_mpy.c

SRC_QSTR += $(SRC_C_AND_QSTR)

# finish up

OBJ += $(PY_O)
OBJ += $(addprefix $(BUILD)/, $(SRC_C:.c=.o))

ifeq ($(QEMU), 1)
all: $(BUILD)/firmware.elf
else
all: $(BUILD)/firmware.udonjson $(BUILD)/firmware.uasm
endif

$(BUILD)/_frozen_mpy.c: $(TOP)/tests/frozen/frozentest.mpy $(BUILD)/genhdr/qstrdefs.generated.h
	$(ECHO) "MISC freezing bytecode"
	$(Q)$(TOP)/tools/mpy-tool.py -f -q $(BUILD)/genhdr/qstrdefs.preprocessed.h -mlongint-impl=longlong $< > $@

ifneq ($(QEMU), 1)
$(BUILD)/firmware.udonjson: $(BUILD)/firmware.elf terminal.ron $(KIP32_SDK)/stdsyscall.ron
	$(KIP32_SDK)/elf2uasm -o $@ $(BUILD)/firmware.elf --udonjson --udonjson-opt --inc terminal.ron
$(BUILD)/firmware.uasm: $(BUILD)/firmware.elf terminal.ron $(KIP32_SDK)/stdsyscall.ron
	$(KIP32_SDK)/elf2uasm -o $@ $(BUILD)/firmware.elf --udonjson-opt --omit-data --ignore-emit-err --inc terminal.ron
endif

$(BUILD)/firmware.elf: $(OBJ)
	$(ECHO) "LINK $@"
	$(Q)$(CC) $(CFLAGS) -o $@ $^
	$(Q)$(SIZE) $@

include $(TOP)/py/mkrules.mk
