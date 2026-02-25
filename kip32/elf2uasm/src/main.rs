use anyhow::*;
use base64::prelude::*;
use kip32ingest::*;
use lexopt::prelude::*;
use std::collections::HashMap;

use kudoninfo::{opcodes, udon_types};

use kudonasm::*;
use kudonast::*;
use kudonodin::*;

mod wrapper;
use wrapper::*;

/// Tries to determine that a string is reasonably safe for inclusion in an Udon symbol.
/// (Doesn't check that the start of a token isn't a digit; for various reasons, you'd have to do that on purpose.)
fn is_udon_safe(x: &str) -> bool {
    for v in x.chars() {
        if v.is_ascii_alphanumeric() {
            continue;
        } else if v == '_' {
            continue;
        }
        return false;
    }
    true
}

fn code_addr(pc: u32, sfx: &str) -> String {
    format!("_code_{:08X}{}", pc, sfx)
}

/// Resolves a fixed jump.
fn resolve_jump(asm: &Wrapper, img: &Sci32Image, to: u32) -> UdonInt {
    if img.is_instruction_at(to) {
        UdonInt::Sym(code_addr(to, ""))
    } else {
        // to prevent assembly failure if this happens to come up, abort
        // this can happen if, say, data is in .text
        // or if the compiler is feeling silly
        asm.comment_c(&format!("# {:08X} mapped to halt", to));
        UdonInt::I(0xFFFFFFFC)
    }
}

// To implement x0 properly:
// 1. `idec` tries to NOP and generally remove x0 writes as much as possible
// 2. Reads from `x0` are transformed into reads from a constant, while writes to `x0` are transformed into writes to a dummy.
// Registers marked with _ are not exported.
// REGISTERS_W has heap indices autocreated; REGISTERS_R does not.
// Except X0, they should match.
const REGISTERS_W: [&'static str; 32] = [
    "_vm_zero_nopwriteshadow",
    "vm_ra",
    "vm_sp",
    "_vm_x3",
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
// keep in sync!!!
const REGISTERS_R: [&'static str; 32] = [
    "_vm_zero", "vm_ra", "vm_sp", "_vm_x3", "_vm_x4", "_vm_t0", "_vm_t1", "_vm_t2",
    // x8/fp
    "_fp", "_s1", // For convenience/sanity, a0-a7 are not marked with any prefix at all.
    "a0", "a1", "a2", "a3", "a4", "a5", // x16
    "a6", "a7", "_vm_s2", "_vm_s3", "_vm_s4", "_vm_s5", "_vm_s6", "_vm_s7", // x24
    "_vm_s8", "_vm_s9", "_vm_s10", "_vm_s11", "_vm_t3", "_vm_t4", "_vm_t5", "_vm_t6",
];

fn resolve_alur(asm: &Wrapper, value: Sci32ALUSource) -> String {
    match value {
        Sci32ALUSource::Immediate(v) => asm.ensure_i32(v as i32),
        Sci32ALUSource::Register(rs) => REGISTERS_R[rs as usize].to_string(),
    }
}

// WORKAROUND:
// We're now at the point where the workarounds themselves have more workarounds.
// System.Buffer.BlockCopy unironically saved performance here, because it's the only way to *efficiently* force certain type conversions.
// The project has been through a few different attempts at doing these, and this is the only decently okay one.
// In any case, there are three kinds of load pipeline...
enum LoadPipe {
    /// 1 extern call; use BitConverter directly
    Word,
    /// 3 extern calls; set to 0, BlockCopy, get from word array
    ZeroPad(i32),
    /// 3 extern calls; BlockCopy, get from array, convert
    Convert {
        holding_cell: &'static str,
        type_size: i32,
        holding_cell_element: &'static str,
        array_get: String,
        converter: String,
    },
}

fn main() -> Result<()> {
    let mut arg_parser = lexopt::Parser::from_env();
    let asm = Wrapper::new();
    let mut img = Sci32Image::default();
    let mut out_filename: Option<std::ffi::OsString> = None;
    let mut inc_files: Vec<String> = Vec::new();
    let mut auto_stack: usize = 0x1000;
    let mut emit_check: bool = true;
    let mut stdsyscall: bool = true;
    let mut udonjson: bool = false;
    while let Some(arg) = arg_parser.next().context("arg_parser")? {
        match arg {
            Short('?') | Short('h') | Long("help") => {
                println!("elf2uasm some.elf");
                println!(" meant to be used with 'microcontroller'-like ELFs");
                println!(" section headers are used, not program headers; but no relocations");
                println!(" passing multiple ELF files is 'supported' but probably isn't helpful");
                println!(" special ELF symbols:");
                println!("  _stack_start: initial value of SP");
                println!("                if not found, stack space will be auto-allocated");
                println!("  (any symbol in .kip32_export): Exports a 'thunk' for easy calling");
                println!("                                 from other Udon code.");
                println!(" additional options:");
                println!(" --out/-o FILE: output .uasm file (default stdout)");
                println!(" --udonjson: use udonjson format");
                println!(" --ignore-emit-err: Ignore emit errors (uasm only)");
                println!(" --no-stdsyscall: By default, stdsyscall.uasm is embedded. Remove it.");
                println!(" --inc FILE: splice this KU2 .ron into the output");
                println!("             see udon/kudonasm/README.md");
                println!("             $KIP32_SDK/stdsyscall.ron is --inc'd first.");
                println!(" --auto-stack WORDS: size of the auto-allocated stack in 32-bit words");
                println!("                     has no effect if _stack_start is found");
                println!(" syscall mechanism notes:");
                println!(" ECALL sets up so that 'JUMP,_vm_indirect_jump' will return to VM.");
                println!(" It then jumps to _ecall. a0-a7 are available as Udon heap variables.");
                println!(" _ecall_vector_table is a uint32 pointing to the syscall table.");
                std::process::exit(0);
            }
            Short('o') | Long("out") => match arg_parser.next() {
                Result::Ok(Some(Value(v))) => out_filename = Some(v),
                _ => return Err(anyhow!("-o/--out expects a filename value")),
            },
            Long("udonjson") => udonjson = true,
            Long("ignore-emit-err") => emit_check = false,
            Long("no-stdsyscall") => stdsyscall = false,
            Long("auto-stack") => match arg_parser.next() {
                Result::Ok(Some(Value(v))) => {
                    auto_stack = v.parse().expect("--auto-stack number should parse")
                }
                _ => return Err(anyhow!("--auto-stack expects a number")),
            },
            Long("inc") => match arg_parser.next() {
                Result::Ok(Some(Value(v))) => {
                    inc_files.push(
                        v.into_string()
                            .expect("--inc arg should convert to string cleanly"),
                    );
                }
                _ => return Err(anyhow!("--inc expects a filename value")),
            },
            Value(v) => {
                let a = std::fs::read(&v).expect(&format!("{:?} reading", v));
                img.from_elf(&a).expect(&format!("{:?} parsing", v));
            }
            _ => return Err(arg.unexpected().into()),
        }
    }
    if stdsyscall {
        let var = std::env::var("KIP32_SDK")
            .expect("if stdsyscall is in use, KIP32_SDK must be set to find it");
        inc_files.insert(0, format!("{}/stdsyscall.ron", var));
    }
    let mut librarian = KU2Librarian::new();
    KU2Package::add_blank(&mut librarian, "inc_code");
    {
        let builtin_parsed =
            kudonasm_parse(&include_str!("builtin.ron")).expect("builtin.ron failed to parse");
        if KU2Package::split(&mut librarian, "BUILTIN", builtin_parsed).len() > 0 {
            panic!("Leftover code in BUILTIN",);
        }
    }
    for v in inc_files {
        let a = std::fs::read_to_string(&v).expect(&format!("{:?} invalid", v));
        let parsed = kudonasm_parse(&a).expect(&format!("{:?} failed to parse", v));
        if KU2Package::split(&mut librarian, &v, parsed).len() > 0 {
            panic!(
                "Leftover code in {:?}. Use inc_code package while we're still using legacy syscall mechanism",
                v
            );
        }
    }
    let initial_sp = match img.symbols.get("_stack_start") {
        Some(initial_sp_sym) => initial_sp_sym.st_addr as i32,
        None => {
            // auto stack
            let new_size = img.data.len() + auto_stack;
            img.data.resize(new_size, 0);
            (new_size * 4) as i32
        }
    };
    // So this is a pretty nonsensical value to have here, probably needs explaining.
    // Basically, we calculate indirect jump addresses as vec * 2 to hit the appropriate points in the jump table.
    // And the Udon abort vector is at 0xFFFFFFFC.
    // Therefore, we can shift that to the right by 1 to get a value which both:
    // 1. Doesn't trigger a not-really-a-bug we have with negative jump addresses
    // 2. Conveniently converts into the Udon abort vector
    // We then jump here and it's all AOK.
    let abort_vec = 0x7FFFFFFE;
    let data = BASE64_STANDARD.encode(&img.initialized_bytes());

    KU2Package::assemble_by_name(
        &mut asm.ku2.borrow_mut(),
        &mut asm.asm(),
        &librarian,
        "builtin_globals",
    )
    .expect("builtin_globals install should succeed");

    asm.declare_heap_i(
        &"_ecall_vector_table",
        UdonAccess::Symbol,
        OdinIntType::UInt,
        (img.instructions * 8) as i64,
    )
    .unwrap();
    let highbit = asm.ensure_i32(0x80000000u32 as i32);
    asm.declare_heap(
        &"_vm_initdata",
        UdonAccess::Symbol,
        &udon_types::SystemString,
        OdinPrimitive::String(data),
    )
    .unwrap();

    asm.ku2()
        .equates
        .insert("_vm_equ_initsp".to_string(), UdonInt::I(initial_sp as i64));
    asm.ku2()
        .equates
        .insert("_vm_equ_abort".to_string(), UdonInt::I(abort_vec as i64));
    asm.ku2().equates.insert(
        "_vm_equ_image_bytes".to_string(),
        UdonInt::I((img.data.len() * 4) as i64),
    );
    for i in 0..32 {
        let authentic = REGISTERS_W[i];
        asm.declare_heap_i(
            &authentic,
            if authentic.starts_with("_") {
                UdonAccess::Symbol
            } else {
                UdonAccess::Public
            },
            OdinIntType::Int,
            0,
        )
        .unwrap();
    }

    let constn1 = asm.ensure_i32(-1);
    let const0 = asm.ensure_i32(0);
    let const1 = asm.ensure_i32(1);

    asm.comment_c("-- JUMP TABLE (MUST BE AT START OF CODE) --");
    for i in 0..img.instructions {
        let pc = (i * 4) as u32;
        uasm_op!(asm.asm(), JUMP, code_addr(pc, ""));
    }
    asm.comment_c("");

    asm.comment_c("-- SYSCALL CODE --");
    KU2Package::assemble_by_name(&mut asm.ku2(), &mut asm.asm(), &librarian, "inc_code")
        .expect("inc_code install should succeed");
    asm.comment_c("");

    KU2Package::assemble_by_name(
        &mut asm.ku2(),
        &mut asm.asm(),
        &librarian,
        "builtin_vm_reset",
    )
    .expect("inc_code install should succeed");

    let mut symbol_marking: HashMap<u32, String> = HashMap::new();
    asm.comment_c(" -- THUNKS --");
    for sym in img.symbols.values() {
        if !is_udon_safe(&sym.st_name) {
            continue;
        }
        symbol_marking.insert(sym.st_addr, sym.st_name.clone());
        if sym.export_section {
            let cut_name = sym.st_name.clone();
            let fastpath_name = format!("_thunk_{}_fastpath", cut_name);

            asm.code_label(&cut_name, UdonAccess::Public).unwrap();
            // check if the VM has inited; if it has, we speedrun init
            asm.obj_equality("_vm_memory_chk", "_null", "_vm_tmp_bool");
            asm.jump_if_false_static("_vm_tmp_bool", &fastpath_name);

            // Slowpath: The machine hasn't been setup yet!
            // Set indirect jump target and then run Reset-And-Jump.
            asm.u32_fromi32(
                asm.ensure_i32(sym.st_addr as i32),
                "vm_indirect_jump_target",
            );
            asm.jump("_vm_reset_and_jump");

            // Fastpath: Machine is ready. Copy registers and directly jump into code.
            asm.code_label(&fastpath_name, UdonAccess::Symbol).unwrap();
            // Setup registers...
            asm.copy_static("vm_initsp", "vm_sp");
            asm.copy_static("vm_initra", "vm_ra");
            // Jump directly into the code.
            asm.jump_ui(resolve_jump(&asm, &img, sym.st_addr as u32));
        }
    }
    asm.comment_c("");

    // ClientSim UI shows symbols in declaration order (presumably Udon preserves this somehow and we're seeing the results)
    // For this reason, make sure that symbol getters are written after the actual important stuff
    asm.comment_c(" -- SYMGET --");
    for sym in img.symbols.values() {
        if !is_udon_safe(&sym.st_name) {
            continue;
        }
        // either way, export as a 'data symbol'
        asm.code_label(&format!("_sym_{}", sym.st_name), UdonAccess::Public)
            .unwrap();
        // just set this now
        asm.copy_static(&asm.ensure_i32(sym.st_addr as i32), "a0");

        // if the VM hasn't inited, we should probably init it
        asm.obj_equality("_vm_memory_chk", "_null", "_vm_tmp_bool");
        // if it's not null, the machine already inited and we can stop now.
        uasm_op!(asm.asm(), PUSH, "_vm_tmp_bool");
        asm.asm().code.push(UdonInt::Op(&opcodes::JUMP_IF_FALSE));
        asm.asm().code.push(UdonInt::I(0xFFFFFFFC));
        // jump to _vm_reset, which sets the indirect jump target to the true abort vector.
        asm.jump("_vm_reset");
    }
    asm.comment_c("");

    asm.comment_c("-- CODE --");
    for i in 0..img.instructions {
        let pc = (i * 4) as u32;
        if let Some(sym) = symbol_marking.get(&pc) {
            asm.comment_c("");
            asm.comment_c(&format!("SYMBOL: {}", sym));
        }
        let istr = Sci32Instr::decode(pc, img.data[i]);
        asm.comment_c(&format!("{:?}", istr));
        asm.code_label(&code_addr(pc, ""), UdonAccess::Symbol)
            .unwrap();
        match istr {
            Sci32Instr::JumpAndLink {
                rd,
                rd_value,
                value,
            } => {
                if rd != Kip32Reg::Zero {
                    asm.copy_static(&asm.ensure_i32(rd_value as i32), REGISTERS_W[rd as usize]);
                }
                asm.jump_ui(resolve_jump(&asm, &img, value));
            }
            Sci32Instr::JumpAndLinkRegister {
                rd,
                rd_value,
                rs1,
                offset,
            } => {
                let si = REGISTERS_R[rs1 as usize].to_string();
                if offset == 0 {
                    asm.u32_fromi32(si, "vm_indirect_jump_target");
                } else {
                    asm.i32_add(si, asm.ensure_i32(offset as i32), "_vm_tmp_r1");
                    asm.u32_fromi32("_vm_tmp_r1", "vm_indirect_jump_target");
                }
                if rd != Kip32Reg::Zero {
                    asm.copy_static(&asm.ensure_i32(rd_value as i32), REGISTERS_W[rd as usize]);
                }
                asm.jump("_vm_indirect_jump");
            }
            Sci32Instr::SetRegister { rd, value } => {
                asm.copy_static(&resolve_alur(&asm, value), REGISTERS_W[rd as usize]);
            }
            Sci32Instr::NOP => {
                uasm_op!(asm.asm(), NOP);
            }
            Sci32Instr::Load {
                rd,
                rs1,
                kind,
                offset,
            } => {
                let mut s_addr = REGISTERS_R[rs1 as usize].to_string();
                let dst = REGISTERS_W[rd as usize].to_string();
                if offset != 0 {
                    let adj = asm.ensure_i32(offset as i32);
                    if rs1 == Kip32Reg::Zero {
                        s_addr = adj;
                    } else {
                        asm.i32_add(s_addr, adj, "_vm_tmp_r1");
                        s_addr = "_vm_tmp_r1".to_string();
                    }
                }
                let pipe = match kind {
                    Sci32LSType::Word => LoadPipe::Word,
                    Sci32LSType::Byte(true) => LoadPipe::ZeroPad(1),
                    Sci32LSType::Half(true) => LoadPipe::ZeroPad(2),
                    Sci32LSType::Byte(false) => LoadPipe::Convert {
                        holding_cell: "_vm_bcopy_i8",
                        type_size: 1,
                        holding_cell_element: "_vm_tmp_i8",
                        array_get: asm.i8array_get.clone(),
                        converter: asm.i32_fromi8.clone(),
                    },
                    Sci32LSType::Half(false) => LoadPipe::Convert {
                        holding_cell: "_vm_bcopy_i16",
                        type_size: 2,
                        holding_cell_element: "_vm_tmp_i16",
                        array_get: asm.i16array_get.clone(),
                        converter: asm.i32_fromi16.clone(),
                    },
                };
                match pipe {
                    LoadPipe::Word => asm.read_i32("vm_memory", s_addr, dst),
                    LoadPipe::ZeroPad(size) => {
                        // Initialize the target to 0 (the zero-padding).
                        asm.i32array_set("_vm_bcopy_i32", &const0, &const0);
                        // Copy in the starting bytes (which are also the low bytes aka what we want).
                        asm.bcopy(
                            "vm_memory",
                            s_addr,
                            "_vm_bcopy_i32",
                            &const0,
                            asm.ensure_i32(size),
                        );
                        // Retrieve the result, which has just been zero-padded.
                        asm.i32array_get("_vm_bcopy_i32", &const0, dst);
                    }
                    LoadPipe::Convert {
                        holding_cell,
                        type_size,
                        holding_cell_element,
                        array_get,
                        converter,
                    } => {
                        // Copy in the target.
                        asm.bcopy(
                            "vm_memory",
                            s_addr,
                            holding_cell,
                            &const0,
                            asm.ensure_i32(type_size),
                        );
                        // Pull from array.
                        uasm_op!(asm.asm(), PUSH, holding_cell);
                        uasm_op!(asm.asm(), PUSH, &const0);
                        uasm_op!(asm.asm(), PUSH, holding_cell_element);
                        uasm_op!(asm.asm(), EXTERN, array_get);
                        // Convert.
                        uasm_op!(asm.asm(), PUSH, holding_cell_element);
                        uasm_op!(asm.asm(), PUSH, dst);
                        uasm_op!(asm.asm(), EXTERN, converter);
                    }
                }
            }
            Sci32Instr::Store {
                rs1,
                rs2,
                kind,
                offset,
            } => {
                let mut s_addr = REGISTERS_R[rs1 as usize].to_string();
                let s_value = REGISTERS_R[rs2 as usize].to_string();
                if offset != 0 {
                    let adj = asm.ensure_i32(offset as i32);
                    if rs1 == Kip32Reg::Zero {
                        s_addr = adj;
                    } else {
                        asm.i32_add(s_addr, adj, "_vm_tmp_r1");
                        s_addr = "_vm_tmp_r1".to_string();
                    }
                }
                // New writer works like this:
                // Register -> Scratch Buffer -> Destination
                // This is a constant 2 externs, which was the best-case number before.
                // Code is also obviously much cleaner.
                let length = match kind {
                    Sci32LSType::Byte(_) => 1,
                    Sci32LSType::Half(_) => 2,
                    Sci32LSType::Word => 4,
                };
                asm.i32array_set("_vm_bcopy_i32", &const0, s_value);
                asm.bcopy(
                    "_vm_bcopy_i32",
                    &const0,
                    "vm_memory",
                    &s_addr,
                    asm.ensure_i32(length),
                );
            }
            Sci32Instr::Branch {
                rs1,
                rs2,
                kind,
                value,
            } => {
                let mut s1 = REGISTERS_R[rs1 as usize].to_string();
                let mut s2 = REGISTERS_R[rs2 as usize].to_string();
                if kind == Sci32BranchType::BLTU || kind == Sci32BranchType::BGEU {
                    // was copy/pasted to SLTU code
                    asm.i32_xor(s1, &highbit, "_vm_tmp_r1");
                    asm.i32_xor(s2, &highbit, "_vm_tmp_r2");
                    s1 = "_vm_tmp_r1".to_string();
                    s2 = "_vm_tmp_r2".to_string();
                }
                // Must be inverted.
                let comptype = match kind {
                    Sci32BranchType::BEQ => &asm.i32_neq,
                    Sci32BranchType::BNE => &asm.i32_eq,
                    Sci32BranchType::BLT => &asm.i32_ge,
                    Sci32BranchType::BGE => &asm.i32_lt,
                    // The above conversion will make this work correctly re: signedness.
                    Sci32BranchType::BLTU => &asm.i32_ge,
                    Sci32BranchType::BGEU => &asm.i32_lt,
                };
                uasm_op!(asm.asm(), PUSH, s1);
                uasm_op!(asm.asm(), PUSH, s2);
                uasm_op!(asm.asm(), PUSH, "_vm_tmp_bool");
                uasm_op!(asm.asm(), EXTERN, comptype);
                asm.jump_if_false_static_ui("_vm_tmp_bool", resolve_jump(&asm, &img, value));
            }
            Sci32Instr::ALU(alu) => {
                let mut s1 = resolve_alur(&asm, alu.s1);
                let mut s2 = resolve_alur(&asm, alu.s2);
                let rd = REGISTERS_W[alu.rd as usize];
                let trivial: Option<&str> = match alu.kind {
                    Sci32ALUType::ADD => Some(&asm.i32_add),
                    Sci32ALUType::SUB => Some(&asm.i32_sub),
                    Sci32ALUType::XOR => Some(&asm.i32_xor),
                    Sci32ALUType::OR => Some(&asm.i32_or),
                    Sci32ALUType::AND => Some(&asm.i32_and),
                    _ => None,
                };
                if let Some(trivial) = trivial {
                    uasm_op!(asm.asm(), PUSH, s1);
                    uasm_op!(asm.asm(), PUSH, s2);
                    uasm_op!(asm.asm(), PUSH, rd);
                    uasm_op!(asm.asm(), EXTERN, trivial);
                } else {
                    match alu.kind {
                        // PERFORMANCE TRICK: So to make this performant, we really have to push the envelope on how much we abuse quirks.
                        // RISC-V wants us to AND off the upper bits of the shift amount.
                        // Luckily, C# will also do this, as evidenced by entering `-2 >> 32` and `-2 << 32` into `csi`.
                        // Therefore, for the simplest cases, SRA and SLL, we can consider those done trivially.
                        // We implement them here for clear reference.
                        Sci32ALUType::SRA => {
                            asm.i32_shr(s1, s2, rd);
                        }
                        Sci32ALUType::SLL => {
                            asm.i32_shl(s1, s2, rd);
                        }
                        // WORKAROUND: SRL, however, is COMPLICATED.
                        // Really, really complicated.
                        // We can't just convert to unsigned U32 because System.Convert hates us, and we can't get it back cleanly either.
                        // (We could use memory tricks not perfected yet at the time of writing, but I want to get rewrite 1 done first before I do rewrite 2.)
                        // The good news is that for a constant shift, we can calculate a mask and simply apply it afterwards.
                        // Two-extern job, even smaller than the U32 code actually.
                        // The bad news is that for something that isn't a constant shift, we're now down one mask.
                        // Looking over the options, the course is clear - we SRA down 0x80000000 by the same shift amount.
                        // The maximum shift is 31, so that extra bit won't get lost; we have a clean set of 32 unique values from the original down to 0xFFFFFFFF.
                        // We then shift it up by 1, taking the mask from 1-32 set upper bits to 0-31, covering solely the area that may have been sign-extended.
                        // We can now proceed to invert the mask and use it.
                        // (We also do this whole mess in immediate calcs.)
                        Sci32ALUType::SRL => {
                            asm.i32_shr(s1, &s2, "_vm_tmp_r1");
                            let mask_src = if let Sci32ALUSource::Immediate(s2v) = alu.s2 {
                                let mut mask: i32 = 0x80000000u32 as i32;
                                mask >>= s2v & 0x1F;
                                mask <<= 1;
                                mask ^= -1;
                                asm.ensure_i32(mask)
                            } else {
                                asm.i32_and(&s2, asm.ensure_i32(0x1F), "_vm_tmp_r2");
                                asm.i32_shr(
                                    asm.ensure_i32(0x80000000u32 as i32),
                                    "_vm_tmp_r2",
                                    "_vm_tmp_r2",
                                );
                                asm.i32_shl("_vm_tmp_r2", &const1, "_vm_tmp_r2");
                                asm.i32_xor("_vm_tmp_r2", &constn1, "_vm_tmp_r2");
                                "_vm_tmp_r2".to_string()
                            };
                            asm.i32_and("_vm_tmp_r1", mask_src, rd);
                        }

                        // SLT is messy. My guess is that it exists to implement certain C operations in a completely branchless manner.
                        // (Consider `int something = a == b;`.)
                        // For example, you can synthesize != as (0 SLTU (A ^ B)) without branching.
                        // As it so happens, System.Convert has an operation which mirrors this, so it can be implemented relatively sanely.
                        Sci32ALUType::SLT(unsigned) => {
                            // These are *obscenely* complicated. You know, as opposed to the shifts...
                            if unsigned {
                                // yup, copy/pasted from above
                                asm.i32_xor(s1, &highbit, "_vm_tmp_r1");
                                asm.i32_xor(s2, &highbit, "_vm_tmp_r2");
                                s1 = "_vm_tmp_r1".to_string();
                                s2 = "_vm_tmp_r2".to_string();
                            }
                            // alright, now actually evaluate
                            asm.i32_lt(s1, s2, "_vm_tmp_bool");
                            asm.i32_frombool("_vm_tmp_bool", rd);
                        }
                        _ => {
                            uasm_stop!(asm.asm());
                        }
                    }
                }
            }
            Sci32Instr::ECALL => {
                // This is for the sake of the ECALL handler.
                // This allows it to return back to normal program execution just using _vm_indirect_jump.
                asm.u32_fromi32(asm.ensure_i32((pc + 4) as i32), "vm_indirect_jump_target");
                uasm_op!(asm.asm(), JUMP, "_ecall");
            }
            // unrecognized, break, etc.
            _ => {
                uasm_stop!(asm.asm());
            }
        }
    }

    // -- final stage --

    let result_text = if udonjson {
        udonprogram_emit_udonjson(&asm.asm.borrow()).expect("emit of udonjson should work").dump()
    } else {
        let uasm_writer = UASMWriter::default();

        let res = udonprogram_emit_uasm(&asm.asm.borrow(), &uasm_writer);
        if emit_check {
            res.expect("emit of UASM should work");
        }
        uasm_writer.to_string()
    };

    if let Some(out_filename) = out_filename {
        std::fs::write(out_filename, result_text)?;
    } else {
        print!("{}", result_text);
    }
    Ok(())
}
