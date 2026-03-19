#include <kip32.h>
#include <kip32_udon.h>
#include "testlibc.h"
#include "muldiv.h"
#include "genrefdata.h"

void putchar(int c) {
	KIP32_SYSCALL1("stdsyscall_putchar", c);
}

int vi_m1 = -1;
int vi_p1 = 1;
unsigned int vu_m1 = (unsigned int) -1;
unsigned int vu_p1 = 1;

int barrier_store;

int barrier(int v) {
	barrier_store = v;
	// this instruction could conceivably do ANYTHING!
	__asm__ volatile ("");
	return barrier_store;
}

int tmp = 0;

short arr_s16[] = {0xE912, 0x4896, 0x8421, 0, 0x8421, 0x8421, 0x8421};
unsigned short vu16 = 0xE912;
signed char arr_s8[] = {0x80, 0x40, 0x10};
unsigned char vu8 = 0xEE;

#define TEST(info, cond) if (cond) { puts(info " OK"); } else { puts(info " FAIL"); }
#define TEST_NOT(info, cond) if (cond) { puts(info " FAIL"); } else { puts(info " OK"); }

KIP32_EXPORT int _interact() {
	puts("science.c : kip32 test program");
	// -- comparison checks --
	TEST("signed LT",             vi_m1 < vi_p1);
	TEST_NOT("signed LTR",        vi_p1 < vi_m1);
	TEST_NOT("unsigned LT",       vu_m1 < vu_p1);
	TEST("unsigned LTR",          vu_p1 < vu_m1);
	// -- SLT checks --
	TEST("signed SLT",    barrier(vi_m1 < vi_p1));
	TEST("signed SLTR",  !barrier(vi_p1 < vi_m1));
	TEST("unsigned SLT", !barrier(vu_m1 < vu_p1));
	TEST("unsigned SLTR", barrier(vu_p1 < vu_m1));
	// -- memory accesses, 16-bit --
	TEST("arr_s16[0]", barrier(arr_s16[0]) == (int) (short) 0xE912);
	TEST("arr_s16[1]", barrier(arr_s16[1]) == 0x4896);
	TEST("arr_s16[2]", barrier(arr_s16[2]) == (int) (short) 0x8421);
	arr_s16[3] = 0x1234;
	arr_s16[5] = 0x5555;
	TEST("arr_s16[3] write", arr_s16[3] == (int) (short) 0x1234);
	TEST("arr_s16[5] write", arr_s16[5] == (int) (short) 0x5555);
	TEST("arr_s16[2]", arr_s16[2] == (int) (short) 0x8421);
	TEST("arr_s16[4]", arr_s16[4] == (int) (short) 0x8421);
	TEST("arr_s16[6]", arr_s16[6] == (int) (short) 0x8421);
	// -- memory accesses, 16-bit unsigned --
	TEST("vu16", barrier(vu16) == 0xE912);
	// -- memory accesses, 8-bit signed --
	TEST("vs8 0", barrier(arr_s8[0]) == -128);
	arr_s8[1] = 0xF3;
	TEST("vs8 1 write", barrier(arr_s8[1]) == (signed char) 0xF3);
	TEST("vs8 2", barrier(arr_s8[2]) == 0x10);
	// -- memory accesses, 8-bit unsigned --
	TEST("vu8", barrier(vu8) == 0xEE);
	// -- maths --
	puts("GRDC testing");
	int caseNumber = 0;
	int casesExecuted = 0;
	const char * grdcVerdict = "fail";
	while (1) {
		const char * caseOpcStr = "?";
		int caseOpc = genrefdata_cases[caseNumber++];
		if (caseOpc == GRDC_END) {
			grdcVerdict = "success";
			break;
		}
		int v1 = genrefdata_cases[caseNumber++];
		int v2 = genrefdata_cases[caseNumber++];
		int vR = genrefdata_cases[caseNumber++];
		int hasResult = 0;
		int resultVal = 0;
		if (caseOpc == GRDC_MUL) {
			caseOpcStr = "GRDC_MUL   ";
			resultVal = muldiv_mul(v1, v2);
			hasResult = 1;
		} else if (caseOpc == GRDC_MULH) {
			caseOpcStr = "GRDC_MULH  ";
		} else if (caseOpc == GRDC_MULHSU) {
			caseOpcStr = "GRDC_MULHSU";
		} else if (caseOpc == GRDC_MULHU) {
			caseOpcStr = "GRDC_MULHU ";
		} else if (caseOpc == GRDC_DIV) {
			caseOpcStr = "GRDC_DIV   ";
			resultVal = muldiv_div(v1, v2);
			hasResult = 1;
		} else if (caseOpc == GRDC_DIVU) {
			caseOpcStr = "GRDC_DIVU  ";
		} else if (caseOpc == GRDC_REM) {
			caseOpcStr = "GRDC_REM   ";
			resultVal = muldiv_rem(v1, v2);
			hasResult = 1;
		} else if (caseOpc == GRDC_REMU) {
			caseOpcStr = "GRDC_REMU  ";
		}
		if (hasResult) {
			casesExecuted++;
			if (resultVal != vR) {
				putsn("GRDC fail: ");
				puthex(caseOpc);
				putsn(", ");
				puthex(v1);
				putsn(", ");
				puthex(v2);
				putsn(", ");
				puthex(vR);
				puts(",");
				break;
			}
		}
	}
	putsn("GRDC testing complete, @");
	puthex(caseNumber);
	putsn(", ");
	puthex(casesExecuted);
	putsn(" cases executed, ");
	puts(grdcVerdict);
	// -- done --
	KIP32_UDON_PUSH("C(string(\"test suite complete\"))");
	KIP32_UDON_EXTERN0("UnityEngineDebug.__Log__SystemObject__SystemVoid");

	tmp++;
	return tmp * tmp;
}

KIP32_EXPORT int increment() {
	return ++tmp;
}
KIP32_EXPORT int decrement() {
	return --tmp;
}
