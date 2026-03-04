. = 0;
SECTIONS {
	/DISCARD/ : {
		*(.comment)
		*(.symtab)
		*(.strtab)
		*(.shstrtab)
		*(.riscv.attributes)
		*(.debug*)
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
	.init : {
		. = ALIGN(4);
		*(.rodata)
		*(.rodata.*)
		. = ALIGN(4);
		_sidata = .; /* Having _sidata and _ebss around is useful for syncing static data. */
		*(.data)
		*(.data.*)
		*(.sdata)
		*(.sdata.*)
	}
	.end : {
		. = ALIGN(4);
		/* uninitialized data */
		*(.bss)
		*(.bss.*)
		*(.sbss)
		*(.sbss.*)
		*(COMMON)
		. = ALIGN(4);
		_ebss = .; /* See above. */
	}
	.kip32_metadata : {
		. = ALIGN(4);
		/* metadata */
		*(.kip32_metadata)
		*(.kip32_metadata.*)
		/* checklinkermap might generate alignment if this is not set, which causes it to become ALLOC (bad) */
		. = ALIGN(4);
	}
	/* If this section is marked ALLOC, check your linker map with -M and adjust this file as necessary */
	.checklinkermap : {
		*(*)
	}
}
