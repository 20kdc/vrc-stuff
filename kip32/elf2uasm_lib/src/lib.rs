use kudonast::{UdonProgram, uasm_op, uasm_op_i};

// To implement x0 properly:
// 1. `idec` tries to NOP and generally remove x0 writes as much as possible
// 2. Reads from `x0` are transformed into reads from a constant, while writes to `x0` are transformed into writes to a dummy.
// Registers marked with _ are not exported.
// REGISTERS_W has heap indices autocreated; REGISTERS_R does not.
// Except X0, they should match.

/// Variables that registers are written to.
/// Keep in sync!!!
pub const REGISTERS_W: [&'static str; 32] = [
    "_vm_zero_nopwriteshadow",
    "_vm_ra",
    "vm_sp",
    "_vm_gp",
    "_vm_x4",
    "_vm_t0",
    "_vm_t1",
    "_vm_t2",
    // x8/fp
    "_fp",
    "_s1",
    // For convenience/sanity, a0-a7 are not marked with any prefix at all.
    "a0",
    "a1",
    "a2",
    "a3",
    "a4",
    "a5", // x16
    "a6",
    "a7",
    "_vm_s2",
    "_vm_s3",
    "_vm_s4",
    "_vm_s5",
    "_vm_s6",
    "_vm_s7",
    // x24
    "_vm_s8",
    "_vm_s9",
    "_vm_s10",
    "_vm_s11",
    "_vm_t3",
    "_vm_t4",
    "_vm_t5",
    "_vm_t6",
];

/// Variables that registers are read from.
/// Keep in sync!!!
pub const REGISTERS_R: [&'static str; 32] = [
    "_vm_zero", "_vm_ra", "vm_sp", "_vm_gp", "_vm_x4", "_vm_t0", "_vm_t1", "_vm_t2",
    // x8/fp
    "_fp", "_s1", // For convenience/sanity, a0-a7 are not marked with any prefix at all.
    "a0", "a1", "a2", "a3", "a4", "a5", // x16
    "a6", "a7", "_vm_s2", "_vm_s3", "_vm_s4", "_vm_s5", "_vm_s6", "_vm_s7", // x24
    "_vm_s8", "_vm_s9", "_vm_s10", "_vm_s11", "_vm_t3", "_vm_t4", "_vm_t5", "_vm_t6",
];

/// Code address label.
pub fn code_addr(pc: u32, sfx: &str) -> String {
    format!("_code_{:08X}{}", pc, sfx)
}

/// Generates the jump table.
pub fn jump_table_gen(asm_mut: &mut UdonProgram, instructions: usize) {
    let code_len = asm_mut.code.len();
    UdonProgram::add_comment(
        &mut asm_mut.code_comments,
        code_len,
        "-- JUMP TABLE (MUST BE AT START OF CODE) --",
    );
    for i in 0..instructions {
        let pc = (i * 4) as u32;
        uasm_op!(asm_mut, JUMP, code_addr(pc, ""));
    }
    // Secret handshake!
    // Technically, any non-JUMP instruction would work.
    uasm_op_i!(asm_mut, ANNOTATION, 0x44657373);
}

/// Reads a generated jump table.
/// If this returns None, the error was in someplace unusual like common code.
/// Your best bet in that event is to assume `vm_indirect_jump_target` is accurate.
pub fn jump_table_lookup(bytecode: &[u32], pc: u32) -> Option<u32> {
    let mut idx: usize = 0;
    let mut best_pc: u32 = 0;
    let mut best_rvpc: Option<u32> = None;
    while idx < (bytecode.len() - 1) {
        if bytecode[idx] != kudoninfo::opcodes::JUMP.opcode {
            break;
        }
        let val = bytecode[idx + 1];
        if val > best_pc && val <= pc {
            best_pc = val;
            best_rvpc = Some((idx * 2) as u32);
        }
        idx += 2;
    }
    best_rvpc
}
