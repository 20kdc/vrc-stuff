#pragma once

/*
 * kip32 minimal incomplete libc
 */

#include <stdint.h>

/* print macros for all bit-sized types */

/*
for _, dt in ipairs({"8", "LEAST8", "FAST8", "16", "LEAST16", "FAST16", "32", "LEAST32", "FAST32", "64", "LEAST64", "FAST64"}) do
local pfx = "" if dt == "64" or dt == "LEAST64" or dt == "FAST64" then pfx = "ll" end
for _, v in ipairs({"d", "i", "o", "u", "x", "X"}) do print("#define PRI" .. v .. dt .. " \"" .. pfx ..  v .. "\"") end end
 */

#define PRId8 "d"
#define PRIi8 "i"
#define PRIo8 "o"
#define PRIu8 "u"
#define PRIx8 "x"
#define PRIX8 "X"
#define PRIdLEAST8 "d"
#define PRIiLEAST8 "i"
#define PRIoLEAST8 "o"
#define PRIuLEAST8 "u"
#define PRIxLEAST8 "x"
#define PRIXLEAST8 "X"
#define PRIdFAST8 "d"
#define PRIiFAST8 "i"
#define PRIoFAST8 "o"
#define PRIuFAST8 "u"
#define PRIxFAST8 "x"
#define PRIXFAST8 "X"
#define PRId16 "d"
#define PRIi16 "i"
#define PRIo16 "o"
#define PRIu16 "u"
#define PRIx16 "x"
#define PRIX16 "X"
#define PRIdLEAST16 "d"
#define PRIiLEAST16 "i"
#define PRIoLEAST16 "o"
#define PRIuLEAST16 "u"
#define PRIxLEAST16 "x"
#define PRIXLEAST16 "X"
#define PRIdFAST16 "d"
#define PRIiFAST16 "i"
#define PRIoFAST16 "o"
#define PRIuFAST16 "u"
#define PRIxFAST16 "x"
#define PRIXFAST16 "X"
#define PRId32 "d"
#define PRIi32 "i"
#define PRIo32 "o"
#define PRIu32 "u"
#define PRIx32 "x"
#define PRIX32 "X"
#define PRIdLEAST32 "d"
#define PRIiLEAST32 "i"
#define PRIoLEAST32 "o"
#define PRIuLEAST32 "u"
#define PRIxLEAST32 "x"
#define PRIXLEAST32 "X"
#define PRIdFAST32 "d"
#define PRIiFAST32 "i"
#define PRIoFAST32 "o"
#define PRIuFAST32 "u"
#define PRIxFAST32 "x"
#define PRIXFAST32 "X"
#define PRId64 "lld"
#define PRIi64 "lli"
#define PRIo64 "llo"
#define PRIu64 "llu"
#define PRIx64 "llx"
#define PRIX64 "llX"
#define PRIdLEAST64 "lld"
#define PRIiLEAST64 "lli"
#define PRIoLEAST64 "llo"
#define PRIuLEAST64 "llu"
#define PRIxLEAST64 "llx"
#define PRIXLEAST64 "llX"
#define PRIdFAST64 "lld"
#define PRIiFAST64 "lli"
#define PRIoFAST64 "llo"
#define PRIuFAST64 "llu"
#define PRIxFAST64 "llx"
#define PRIXFAST64 "llX"

/* scan macros for most bit-sized types -- we can't know what compiler chose for least/fast */

/*
for _, dt in ipairs({"8", "16", "32", "64"}) do
local pfx = "" if dt == "8" then pfx = "hh" end if dt == "16" then pfx = "h" end if dt == "64" then pfx = "ll" end
for _, v in ipairs({"d", "i", "o", "u", "x"}) do print("#define SCN" .. v .. dt .. " \"" .. pfx ..  v .. "\"") end end
 */

#define SCNd8 "hhd"
#define SCNi8 "hhi"
#define SCNo8 "hho"
#define SCNu8 "hhu"
#define SCNx8 "hhx"
#define SCNd16 "hd"
#define SCNi16 "hi"
#define SCNo16 "ho"
#define SCNu16 "hu"
#define SCNx16 "hx"
#define SCNd32 "d"
#define SCNi32 "i"
#define SCNo32 "o"
#define SCNu32 "u"
#define SCNx32 "x"
#define SCNd64 "lld"
#define SCNi64 "lli"
#define SCNo64 "llo"
#define SCNu64 "llu"
#define SCNx64 "llx"

/* print/scan for max/ptr */

#define PRIdMAX "jd"
#define PRIiMAX "ji"
#define PRIoMAX "jo"
#define PRIuMAX "ju"
#define PRIxMAX "jx"
#define PRIXMAX "jX"

#define SCNdMAX "jd"
#define SCNiMAX "ji"
#define SCNoMAX "jo"
#define SCNuMAX "ju"
#define SCNxMAX "jx"

#define PRIdPTR "d"
#define PRIiPTR "i"
#define PRIoPTR "o"
#define PRIuPTR "u"
#define PRIxPTR "x"
#define PRIXPTR "X"

#define SCNdPTR "d"
#define SCNiPTR "i"
#define SCNoPTR "o"
#define SCNuPTR "u"
#define SCNxPTR "x"

/* intmax functions */

/* This is implemented as an alias in stdlib/llabs.c */
intmax_t imaxabs(intmax_t j);

/* This is implemented as an alias in stdlib/lldiv.c */
typedef struct _imaxdiv_t {
	intmax_t quot;
	intmax_t rem;
} imaxdiv_t;

imaxdiv_t imaxdiv(intmax_t numer, intmax_t denom);

/* These are implemented as aliases in stdlib/strtoll.c and stdlib/strtoull.c */
intmax_t strtoimax(const char * __restrict__ nptr, char ** __restrict__ endptr, int base);
uintmax_t strtoumax(const char * __restrict__ nptr, char ** __restrict__ endptr, int base);
