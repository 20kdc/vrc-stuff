use crate::target::Kip32CoreDump;

pub fn parse(res: &Vec<u8>) -> Result<Kip32CoreDump, String> {
    let entries = kudonodin::OdinEntry::read_all_from_slice(&res).expect("decode must succeed");
    let file = kudonodin::OdinASTFile::from_entries(entries);
    let root_val = file
        .get_root_value()
        .expect("file should have a single root");
    let core_dump: kudonast::UdonCoreDump =
        kudonodin::OdinSTDeserializable::deserialize(&file, root_val).expect("must deserialize");

    let mut workspace = Kip32CoreDump::default();

    // pc
    if let Some(pc) =
        elf2uasm_lib::jump_table_lookup(&core_dump.program.bytecode, core_dump.error_pc)
    {
        workspace.pc = pc;
    } else {
        // happened somewhere odd. try to figure it out
        if let Some(ipc) = core_dump
            .program
            .symbol_table
            .sym_to_addr("vm_indirect_jump_target")
        {
            if let Some(Some((_, ipc))) = core_dump.heap.0.get(ipc as usize) {
                let regval: Result<u32, String> =
                    kudonodin::OdinSTDeserializable::deserialize(&file, ipc);
                if let Ok(regval) = regval {
                    workspace.pc = regval;
                }
            }
        }
    }

    // registers
    for (i, reg) in elf2uasm_lib::REGISTERS_R.iter().enumerate() {
        if let Some(reg) = core_dump.program.symbol_table.sym_to_addr(reg) {
            if let Some(Some((_, reg))) = core_dump.heap.0.get(reg as usize) {
                let regval: Result<i32, String> =
                    kudonodin::OdinSTDeserializable::deserialize(&file, reg);
                if let Ok(regval) = regval {
                    workspace.x[i] = regval as u32;
                }
            }
        }
    }

    // memory
    if let Some(mem) = core_dump.program.symbol_table.sym_to_addr("vm_memory") {
        if let Some(Some((_, mem))) = core_dump.heap.0.get(mem as usize) {
            let memchk: Result<Vec<u8>, String> =
                kudonodin::OdinSTDeserializable::deserialize(&file, mem);
            if let Ok(memchk) = memchk {
                workspace.memory = memchk;
            }
        }
    }

    Ok(workspace)
}
