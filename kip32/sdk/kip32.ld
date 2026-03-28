. = 0;
SECTIONS {
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
		*(.kip32_metadata)
		*(.kip32_metadata.*)
		/* checklinkermap might generate alignment if this is not set, which causes it to become ALLOC (bad) */
		. = ALIGN(4);
	}
	/*
	 * Used to be exhaustive about deleting or moving sections, but this causes issues with debugging metadata.
	 * Check objdump -h, okay?
	 */
}
