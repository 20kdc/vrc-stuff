#include <kip32_udon.h>
#include <stdio.h>

// We continue with an example of some KU2 inline assembly.
// There's various shorthand available (see the kudonasm documentation).
// But to get the format across, we avoid some of it here.

// Udon fields can be declared:
KIP32_UDON_GLOBALASM(arbitrary_id, "var(mystring, string(\"Hello, world!\"))")

KIP32_EXPORT void _start() {
	// And then referred to in the program code:
	KIP32_UDON_ASM0(
		"push(mystring)\n"
		"extern(EXT(\"UnityEngineDebug.__Log__SystemObject__SystemVoid\"))"
	)
	// A full list of Udon's externs is provided in the vrc-stuff repository.
	int v = 123;
	// Registers a0-a7 (arguments) are available as variables (read/write).
	// They are always of type SystemInt32; this mustn't change.
	KIP32_UDON_ASM1(
		"push(a0)\n"
		"extern(EXT(\"UnityEngineDebug.__Log__SystemObject__SystemVoid\"))"
		, v
	)
}
