#pragma once

#define LENGTH_FLAGS_L1 1
#define LENGTH_FLAGS_L2 2
#define LENGTH_FLAGS_H1 4
#define LENGTH_FLAGS_H2 8
#define LENGTH_FLAGS_J 16
#define LENGTH_FLAGS_Z 32
#define LENGTH_FLAGS_T 64
#define LENGTH_FLAGS_LD 128

int __kip32_libc_lengthflags_tryconsume(int * lengthFlags, char chr);

void __kip32_libc_lengthflags_write(int lengthFlags, void * target, long long value);
