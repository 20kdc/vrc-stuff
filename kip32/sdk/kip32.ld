. = 0;
SECTIONS {
	/DISCARD/ : {
		/*
		 * destructors
		 * we wish to **aggressively** lose these particular sections
		 */
		*(.fini_array .dtors .dtors.*)
	}
	.null : {
		/*
		 * We leave an unrecognized opcode here to catch calls to null pointers.
		 */
		LONG(0x00000000)
	}
	.kip32_export : {
		. = ALIGN(4);
		KEEP(*(.kip32_export))
		KEEP(*(.kip32_export.*))
	}
	.text : {
		. = ALIGN(4);
		*(.text)
		*(.text.*)
	}
	/* -- executable instruction barrier -- */
	.init : {
		. = ALIGN(4);
		*(.srodata .srodata.*)
		*(.rodata .rodata.* .gnu.linkonce.r.* .rodata1)
		/* put this here just so it's... somewhere idk */
		*(.eh_frame .eh_frame.*)
		. = ALIGN(4);
		_sidata = .; /* Having _sidata and _ebss around is useful for syncing static data. */
		*(.sdata .sdata.* .gnu.linkonce.s.*)
		*(.sdata2 .sdata2.* .gnu.linkonce.s2.*)
		*(.data .data.* .gnu.linkonce.d.*)

		/* constructors ; not presently used, but... */
		__init_array_start = .;
		KEEP(*(.preinit_array))
		KEEP(*(SORT_BY_INIT_PRIORITY(.init_array.*) SORT_BY_INIT_PRIORITY(.ctors.*)))
		KEEP(*(.init_array .ctors))
		__init_array_end = .;
	}
	.end : {
		. = ALIGN(4);
		/* uninitialized data */
		*(.dynsbss)
		*(.sbss .sbss.* .gnu.linkonce.sb.*)
		*(.scommon)
		*(.dynbss)
		*(.bss .bss.* .gnu.linkonce.b.*)
		*(COMMON)
		. = ALIGN(4);
		/* _ebss used to be provided, but it's useful for qemu for the riscv-unknown-elf linker script to be completely unaltered, and it doesn't provide it. */
		_end = .;
	}
	.kip32_metadata : {
		. = ALIGN(4);
		/* metadata */
		KEEP(*(.kip32_metadata .kip32_metadata.*))
		/* checklinkermap might generate alignment if this is not set, which causes it to become ALLOC (bad) */
		. = ALIGN(4);
	}
	/*
	 * Used to be exhaustive about deleting or moving sections, but this causes issues with debugging metadata.
	 * Check objdump -h, okay?
	 */
}
