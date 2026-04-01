use anyhow::*;
use base64::prelude::*;
use kip32ingest::*;
use lexopt::prelude::*;
use std::collections::HashMap;

use kudoninfo::{opcodes, udontyperef};

use kudonasm::*;
use kudonast::*;
use kudonodin::*;

use elf2uasm_lib::*;

mod wrapper;
use wrapper::*;

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

fn resolve_alur(asm: &Wrapper, value: Sci32ALUSource) -> String {
    match value {
        Sci32ALUSource::Immediate(v) => asm.ensure_i32(v as i32),
        Sci32ALUSource::Register(rs) => REGISTERS_R[rs as usize].to_string(),
    }
}

/// Note that the fallthrough jump code is generated after this if necessary.
fn asm_syscall(asm: &Wrapper, img: &Sci32Image, name: &str, istr_jump: u32) {
    let jmp = resolve_jump(&asm, &img, istr_jump);
    if let Some(sfx) = name.strip_prefix("builtin_extern_") {
        uasm_op!(asm.asm(), EXTERN, asm.asm().ensure_string(sfx, true));
        return;
    } else if let Some(sfx) = name.strip_prefix("builtin_push_") {
        if let Result::Ok(v) = kudonasm_parse_operand(sfx) {
            let ui = asm.ku2().operand_udonint(&mut asm.asm(), &v);
            if let Result::Ok(r) = ui {
                uasm_op!(asm.asm(), PUSH);
                asm.asm().code.push(r);
                return;
            }
        }
    }
    // -- all from now on use snippet_equates
    let snippet_equates = Some(vec![
        (format!("_syscall_return"), jmp),
        (
            format!("_syscall_return_indirect"),
            UdonInt::I(istr_jump as i64),
        ),
    ]);
    if let Some(sfx) = name.strip_prefix("builtin_asm:") {
        // We simply accept that we'll have to abort on a misparse here.
        // This ought to be extremely unlikely, though, as the prefixes and presence in the metadata guarantees some level of error checking.
        let mut parsed =
            kudonasm_parse(sfx).expect(&format!("builtin_asm did not parse: {:?}", sfx));
        let parsed: Vec<(String, KU2Instruction)> = parsed
            .drain(..)
            .map(|v| (format!("builtin_asm:{}", v.0), v.1))
            .collect();
        asm.ku2()
            .snippet_invoke_anonymous(&mut asm.asm(), &parsed, snippet_equates)
            .expect("builtin_asm should assemble");
        return;
    }
    let syscall_package = format!("syscall_{}", name);
    // just in case all the other measures failed, the syscall must exist
    let package_exists = asm.ku2().packages.contains_key(&syscall_package);
    if package_exists {
        asm.ku2()
            .snippet_invoke(&mut asm.asm(), &syscall_package, snippet_equates)
            .expect("syscall should assemble");
    } else {
        // don't mention which, just in case it breaks UASM writer
        asm.comment_c(&format!("WARN: syscall does not exist"));
        eprintln!(
            "Code looked like it referred to syscall '{}', but it didn't exist.",
            name
        );
        uasm_op!(
            asm.asm(),
            EXTERN,
            asm.asm()
                .ensure_string(&format!("Kip32.MissingSyscall {}", name), true)
        );
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
    /// 2 extern calls; get, convert to i32
    BitConverter {
        holding_cell_element: &'static str,
        /// (memory, addr, result) -- also works for u8array_get
        reader: String,
        converter: String,
    },
    /// 3 extern calls; BlockCopy, get from array, convert
    Convert {
        holding_cell: &'static str,
        type_size: i32,
        holding_cell_element: &'static str,
        array_get: String,
        converter: String,
    },
}

/// Generates bitcasting an i64 to an i32 via the bcopy system.
/// Notably, 'upper' can be set to true, which gets the upper rather than lower 32-bits.
fn gen_i64_bitcast_i32(asm: &Wrapper, vi64: &str, rd: &str, upper: bool) {
    let const0 = asm.ensure_i32(0);
    let src_ofs = if upper {
        asm.ensure_i32(4)
    } else {
        const0.clone()
    };
    asm.i64array_set("_vm_bcopy_i64", &const0, vi64);
    asm.bcopy(
        "_vm_bcopy_i64",
        &src_ofs,
        "_vm_bcopy_i32",
        &const0,
        &asm.ensure_i32(4),
    );
    asm.i32array_get("_vm_bcopy_i32", &const0, rd);
}

fn main() -> Result<()> {
    let mut arg_parser = lexopt::Parser::from_env();
    let asm = Wrapper::new();
    let mut img = Sci32Image::default();
    let mut out_filename: Option<std::ffi::OsString> = None;
    let mut inc_files: Vec<String> = Vec::new();
    let mut auto_stack: usize = 0x1000;
    let mut emit_check: bool = true;
    let mut omit_data: bool = false;
    let mut stdsyscall: bool = true;
    let mut udonjson: bool = false;
    let mut udonjson_opt: bool = false;
    while let Some(arg) = arg_parser.next().context("arg_parser")? {
        match arg {
            Short('?') | Short('h') | Long("help") => {
                //        12345678901234567890123456789012345678901234567890123456789012345678901234567890
                println!("elf2uasm some.elf");
                println!(" meant to be used with 'microcontroller'-like ELFs");
                println!(" additional options:");
                println!(" --out/-o FILE: output .uasm file (default stdout)");
                println!(" --udonjson: use udonjson format");
                println!(" --ignore-emit-err: Ignore emit errors (uasm only)");
                println!(" --omit-data: Omits data. Unrunnable output. (for reference/debug)");
                println!(" --udonjson-opt: Emit udonjson-only code. UASM unrunnable.");
                println!(" --no-stdsyscall: By default, stdsyscall.uasm is embedded. Remove it.");
                println!(" --inc FILE: splice this KU2 .ron into the output");
                println!("             see udon/kudonasm/README.md");
                println!("             $KIP32_SDK/stdsyscall.ron is --inc'd first.");
                println!(" --auto-stack WORDS: size of the auto-allocated stack in 32-bit words");
                println!("                     has no effect if _stack_start is found");
                std::process::exit(0);
            }
            Short('o') | Long("out") => match arg_parser.next() {
                Result::Ok(Some(Value(v))) => out_filename = Some(v),
                _ => return Err(anyhow!("-o/--out expects a filename value")),
            },
            Long("udonjson") => udonjson = true,
            Long("udonjson-opt") => udonjson_opt = true,
            Long("ignore-emit-err") => emit_check = false,
            Long("omit-data") => omit_data = true,
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

    // -- parse includes --
    {
        let builtin_parsed =
            kudonasm_parse(&include_str!("builtin.ron")).expect("builtin.ron failed to parse");
        asm.ku2()
            .assemble_file(&mut asm.asm(), "BUILTIN", &builtin_parsed)
            .expect("builtin.ron failed to assemble");
    }
    for v in inc_files {
        let a = std::fs::read_to_string(&v).expect(&format!("{:?} invalid", v));
        let parsed = kudonasm_parse(&a).expect(&format!("{:?} failed to parse", v));
        asm.ku2()
            .assemble_file(&mut asm.asm(), &v, &parsed)
            .expect(&format!("{:?} failed to assemble", v));
    }

    // -- it begins --
    let initial_sp = img.initial_sp(auto_stack);
    let initial_gp = img.initial_gp();
    // So this is a pretty nonsensical value to have here, probably needs explaining.
    // Basically, we calculate indirect jump addresses as vec * 2 to hit the appropriate points in the jump table.
    // And the Udon abort vector is at 0xFFFFFFFC.
    // Therefore, we can shift that to the right by 1 to get a value which both:
    // 1. Doesn't trigger a not-really-a-bug we have with negative jump addresses
    // 2. Conveniently converts into the Udon abort vector
    // We then jump here and it's all AOK.
    let abort_vec = 0x7FFFFFFE;
    if udonjson_opt {
        asm.ku2().equates.insert(
            "_vmext_initdata_to_initdata_dec".to_string(),
            UdonInt::Sym(asm.u8array_clone.clone()),
        );
        let mut data = img.initialized_bytes();
        if omit_data {
            data.clear();
        }
        asm.declare_heap(
            &"_vm_initdata",
            Some(UdonAccess::Symbol),
            udontyperef!(SystemByteArray),
            UdonHeapValue::PrimitiveArray(
                udontyperef!(SystemByteArray),
                OdinPrimitiveArray::U8(data),
            ),
        )
        .unwrap();
    } else {
        let data = if omit_data {
            "[data omitted using --omit-data]".to_string()
        } else {
            BASE64_STANDARD.encode(&img.initialized_bytes())
        };
        // Allows for possible fastpath
        asm.ku2().equates.insert(
            "_vmext_initdata_to_initdata_dec".to_string(),
            UdonInt::Sym(asm.base64_decode.clone()),
        );
        asm.declare_heap(
            &"_vm_initdata",
            Some(UdonAccess::Symbol),
            udontyperef!(SystemString),
            OdinPrimitive::String(data),
        )
        .unwrap();
    }

    asm.ku2()
        .install(&mut asm.asm(), "builtin_globals")
        .expect("builtin_globals install should succeed");

    let highbit = asm.ensure_i32(0x80000000u32 as i32);

    asm.ku2()
        .equates
        .insert("_vm_equ_initsp".to_string(), UdonInt::I(initial_sp as i64));
    asm.ku2()
        .equates
        .insert("_vm_equ_initgp".to_string(), UdonInt::I(initial_gp as i64));
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
                Some(UdonAccess::Symbol)
            } else {
                Some(UdonAccess::Public)
            },
            OdinIntType::Int,
            0,
        )
        .unwrap();
    }

    let constn1 = asm.ensure_i32(-1);
    let const0 = asm.ensure_i32(0);
    let const1 = asm.ensure_i32(1);

    jump_table_gen(&mut asm.asm.borrow_mut(), img.instructions);
    asm.comment_c("");

    asm.comment_c("-- inc_code --");
    asm.ku2()
        .install(&mut asm.asm(), "inc_code")
        .expect("inc_code install should succeed");
    asm.comment_c("");

    asm.comment_c("-- asm metadata --");
    for v in &img.metadata_strings {
        if let Some(asm_src) = v.strip_prefix("udon_asm:") {
            let parsed = kudonasm_parse(&asm_src).expect(&format!("{:?} failed to parse", v));
            asm.ku2()
                .assemble_file(&mut asm.asm(), &"udon_asm metadata", &parsed)
                .expect(&format!("{:?} failed to assemble", v));
        }
    }
    asm.comment_c("");

    asm.comment_c("-- syscall packages --");
    for i in 0..img.instructions {
        let pc = (i * 4) as u32;
        let istr = Kip32FusedInstr::read_fuse(&img, pc);
        if let Kip32FIC::NametableSyscall(name) = istr.content {
            let syscall_package = format!("syscall_{}", name);
            // just in case all the other measures failed, the syscall must exist
            let package_exists = asm.ku2().packages.contains_key(&syscall_package);
            if package_exists {
                asm.ku2()
                    .install_deps(&mut asm.asm(), &syscall_package)
                    .expect("syscall deps assembly must succeed");
            }
        }
    }
    asm.comment_c("");

    asm.ku2()
        .install(&mut asm.asm(), "builtin_vm_reset")
        .expect("builtin_vm_reset install should succeed");

    let mut symbol_marking: HashMap<u32, String> = HashMap::new();
    asm.comment_c(" -- THUNKS --");
    for sym in img.symbols.values() {
        if !UASMWriter::is_udon_safe(&sym.st_name) {
            continue;
        }
        symbol_marking.insert(sym.st_addr, sym.st_name.clone());
        if sym.export_section {
            let cut_name = sym.st_name.clone();

            asm.code_label(&cut_name, Some(UdonAccess::Public)).unwrap();
            asm.ku2()
                .snippet_invoke(
                    &mut asm.asm(),
                    "builtin_thunk",
                    Some(vec![
                        (format!("_thunk_address"), UdonInt::I(sym.st_addr as i64)),
                        (
                            format!("_thunk_jump"),
                            resolve_jump(&asm, &img, sym.st_addr as u32),
                        ),
                    ]),
                )
                .expect("builtin_thunk should assemble");
        }
    }
    asm.comment_c("");

    // ClientSim UI shows symbols in declaration order (presumably Udon preserves this somehow and we're seeing the results)
    // For this reason, make sure that symbol getters are written after the actual important stuff
    asm.comment_c(" -- SYMGET --");
    for sym in img.symbols.values() {
        if !UASMWriter::is_udon_safe(&sym.st_name) {
            continue;
        }
        // either way, export as a 'data symbol'
        asm.code_label(&format!("_sym_{}", sym.st_name), Some(UdonAccess::Public))
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
        let istr = Kip32FusedInstr::read_fuse(&img, pc);
        asm.comment_c(&format!("{:?}", istr.content));
        asm.code_label(&code_addr(pc, ""), Some(UdonAccess::Elidable))
            .unwrap();

        // We add a jump unless this is true.
        // This ensures that we jump over the other instructions in a fused instruction.
        // Notably, if the instruction is a guaranteed branch instruction, we auto-set this to true.
        // Meanwhile, NOP sets this to false so that we generate at least one instruction.
        let mut fallthrough_ok = istr.fallthrough_ok;

        match istr.content {
            Kip32FIC::I(Sci32Instr::JumpAndLink {
                rd,
                rd_value,
                value,
            }) => {
                if rd != Kip32Reg::Zero {
                    asm.copy_static(&asm.ensure_i32(rd_value as i32), REGISTERS_W[rd as usize]);
                }
                asm.jump_ui(resolve_jump(&asm, &img, value));
                fallthrough_ok = true;
            }
            Kip32FIC::I(Sci32Instr::JumpAndLinkRegister {
                rd,
                rd_value,
                rs1,
                offset,
            }) => {
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
                fallthrough_ok = true;
            }
            Kip32FIC::I(Sci32Instr::SetRegister {
                rd,
                value,
                meta_source: _,
            }) => {
                asm.copy_static(&resolve_alur(&asm, value), REGISTERS_W[rd as usize]);
            }
            Kip32FIC::I(Sci32Instr::NOP(_)) => {
                // Guarantee we generate at least one instruction.
                // This used to just write a NOP, but instruction fusion now uses NOPs for jumps, and will fuse NOP-JUMP.
                fallthrough_ok = false;
            }
            Kip32FIC::I(Sci32Instr::Load {
                rd,
                rs1,
                kind,
                offset,
            }) => {
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
                    Sci32LSType::Byte(true) => LoadPipe::BitConverter {
                        holding_cell_element: "_vm_tmp_u8",
                        reader: asm.u8array_get.clone(),
                        converter: asm.i32_fromu8.clone(),
                    },
                    Sci32LSType::Half(true) => LoadPipe::BitConverter {
                        holding_cell_element: "_vm_tmp_u16",
                        reader: asm.read_u16.clone(),
                        converter: asm.i32_fromu16.clone(),
                    },
                    Sci32LSType::Byte(false) => LoadPipe::Convert {
                        holding_cell: "_vm_bcopy_i8",
                        type_size: 1,
                        holding_cell_element: "_vm_tmp_i8",
                        array_get: asm.i8array_get.clone(),
                        converter: asm.i32_fromi8.clone(),
                    },
                    Sci32LSType::Half(false) => LoadPipe::BitConverter {
                        holding_cell_element: "_vm_tmp_i16",
                        reader: asm.read_i16.clone(),
                        converter: asm.i32_fromi16.clone(),
                    },
                };
                match pipe {
                    LoadPipe::Word => asm.read_i32("vm_memory", s_addr, dst),
                    LoadPipe::BitConverter {
                        holding_cell_element,
                        reader,
                        converter,
                    } => {
                        // Read.
                        uasm_op!(asm.asm(), PUSH, "vm_memory");
                        uasm_op!(asm.asm(), PUSH, s_addr);
                        uasm_op!(asm.asm(), PUSH, holding_cell_element);
                        uasm_op!(asm.asm(), EXTERN, reader);
                        // Convert.
                        uasm_op!(asm.asm(), PUSH, holding_cell_element);
                        uasm_op!(asm.asm(), PUSH, dst);
                        uasm_op!(asm.asm(), EXTERN, converter);
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
            Kip32FIC::I(Sci32Instr::Store {
                rs1,
                rs2,
                kind,
                offset,
            }) => {
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
            Kip32FIC::I(Sci32Instr::Branch {
                rs1,
                rs2,
                kind,
                value,
            }) => {
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
            Kip32FIC::I(Sci32Instr::ALU(alu)) => {
                let mut s1 = resolve_alur(&asm, alu.s1);
                let mut s2 = resolve_alur(&asm, alu.s2);
                let rd = REGISTERS_W[alu.rd as usize];
                let trivial: Option<&str> = match alu.kind {
                    Sci32ALUType::ADD => Some(&asm.i32_add),
                    Sci32ALUType::SUB => Some(&asm.i32_sub),
                    Sci32ALUType::XOR => Some(&asm.i32_xor),
                    Sci32ALUType::OR => Some(&asm.i32_or),
                    Sci32ALUType::AND => Some(&asm.i32_and),
                    // this is the only M extension op that's trivial like this...
                    Sci32ALUType::MUL => Some(&asm.i32_mul),
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

                        // M extension
                        Sci32ALUType::DIVREM(divrem, unsigned) => {
                            let resolved_jmp = resolve_jump(&asm, &img, istr.jump);
                            if !unsigned {
                                // so both signed codepaths go through the standard div/rem.
                                // div DEFINITELY throws overflow exceptions, and rem MIGHT throw them
                                // I may have gotten a little paranoid at this point
                                let divnotoverflow = code_addr(pc, "_divnotoverflow");

                                let constn2pln1 = asm.ensure_i32(0x80000000u32 as i32);
                                asm.i32_eq(&s1, &constn2pln1, "_vm_tmp_bool");
                                asm.jump_if_false_static("_vm_tmp_bool", &divnotoverflow);
                                asm.i32_eq(&s2, &constn1, "_vm_tmp_bool");
                                asm.jump_if_false_static("_vm_tmp_bool", &divnotoverflow);

                                // provide expected overflow results
                                // see table 7.1
                                match divrem {
                                    Kip32DIVREMType::DIV => {
                                        asm.copy_static(&constn2pln1, rd);
                                    }
                                    Kip32DIVREMType::REM => {
                                        asm.copy_static(&const0, rd);
                                    }
                                }
                                asm.jump_ui(resolved_jmp.clone());

                                // it's not overflow, we're safe... to check if it's div0
                                asm.code_label(&divnotoverflow, Some(UdonAccess::Elidable))
                                    .unwrap();
                            }
                            let divok = code_addr(pc, "_divok");

                            // because we want to use fallthrough if possible, we jump if divisor is not 0.
                            asm.i32_eq(&s2, &const0, "_vm_tmp_bool");
                            asm.jump_if_false_static("_vm_tmp_bool", &divok);

                            // if divisor is 0, return appropriate value
                            // see table 7.1
                            match divrem {
                                Kip32DIVREMType::DIV => {
                                    // -1
                                    asm.copy_static(&constn1, rd);
                                }
                                Kip32DIVREMType::REM => {
                                    // dividend
                                    asm.copy_static(&s1, rd);
                                }
                            }
                            // ... and jump to next instruction
                            asm.jump_ui(resolved_jmp);

                            // actual main division/remainder codegen
                            asm.code_label(&divok, Some(UdonAccess::Elidable)).unwrap();
                            match (unsigned, divrem) {
                                (false, Kip32DIVREMType::DIV) => {
                                    asm.i32_div(&s1, &s2, rd);
                                }
                                (false, Kip32DIVREMType::REM) => {
                                    asm.i32_rem(&s1, &s2, rd);
                                }
                                (true, divrem) => {
                                    // you'd **think** that unsigned modulo would be a thing
                                    // it is not, so we have to escalate to 64-bit shenanigans and use Math.DivRem
                                    // load parameters into the scuffed drive, transfer, and retrieve
                                    // we load them in as [s1, 0, s2, 0]
                                    // this is essentially zero-extension done the scuffed way
                                    // you might be wondering why we don't use System.Convert to get the i64
                                    // answer: it loves to throw exceptions, and the i32-to-u32 step would throw
                                    // alternatively, we could upcast now and mask later, but we don't have u64 constants on UASM
                                    // the tldr of all of this is: trust me. I got us this far, didn't I? this is the best way
                                    asm.i32array_set("_vm_bcopy_i32", &const0, &s1);
                                    asm.i32array_set("_vm_bcopy_i32", &const1, &const0);
                                    asm.i32array_set("_vm_bcopy_i32", asm.ensure_i32(2), &s2);
                                    asm.i32array_set("_vm_bcopy_i32", asm.ensure_i32(3), &const0);
                                    asm.bcopy(
                                        "_vm_bcopy_i32",
                                        &const0,
                                        "_vm_bcopy_i64",
                                        &const0,
                                        &asm.ensure_i32(16),
                                    );
                                    asm.i64array_get("_vm_bcopy_i64", &const0, "_vm_tmp_i64_a");
                                    asm.i64array_get("_vm_bcopy_i64", &const1, "_vm_tmp_i64_b");
                                    // perform actual operation
                                    match divrem {
                                        Kip32DIVREMType::DIV => {
                                            asm.i64_div(
                                                "_vm_tmp_i64_a",
                                                "_vm_tmp_i64_b",
                                                "_vm_tmp_i64_a",
                                            );
                                        }
                                        Kip32DIVREMType::REM => {
                                            asm.i64_divrem(
                                                "_vm_tmp_i64_a",
                                                "_vm_tmp_i64_b",
                                                "_vm_tmp_i64_a",
                                                "_vm_tmp_i64_c",
                                            );
                                        }
                                    }
                                    gen_i64_bitcast_i32(&asm, "_vm_tmp_i64_a", rd, false);
                                }
                            }
                        }

                        Sci32ALUType::MULH(mulh) => {
                            // This remapping is important because there's a fastpath for MULHU.
                            let (lhs_signed, rhs_signed) = match mulh {
                                Sci32MULHType::MULH => (true, true),
                                Sci32MULHType::MULHSU => (true, false),
                                Sci32MULHType::MULHU => (false, false),
                            };
                            match (lhs_signed, rhs_signed) {
                                (true, true) => {
                                    // this specific codepath is really simple
                                    asm.i32i64_mul(&s1, &s2, "_vm_tmp_i64_a");
                                }
                                (false, false) => {
                                    // copy/paste of DIV/REMU logic :(
                                    asm.i32array_set("_vm_bcopy_i32", &const0, &s1);
                                    asm.i32array_set("_vm_bcopy_i32", &const1, &const0);
                                    asm.i32array_set("_vm_bcopy_i32", asm.ensure_i32(2), &s2);
                                    asm.i32array_set("_vm_bcopy_i32", asm.ensure_i32(3), &const0);
                                    asm.bcopy(
                                        "_vm_bcopy_i32",
                                        &const0,
                                        "_vm_bcopy_i64",
                                        &const0,
                                        &asm.ensure_i32(16),
                                    );
                                    asm.i64array_get("_vm_bcopy_i64", &const0, "_vm_tmp_i64_a");
                                    asm.i64array_get("_vm_bcopy_i64", &const1, "_vm_tmp_i64_b");
                                    // actual operation
                                    asm.i64_mul("_vm_tmp_i64_a", "_vm_tmp_i64_b", "_vm_tmp_i64_a");
                                }
                                (true, false) => {
                                    // rs1 is signed, so convert directly
                                    asm.i64_fromi32(&s1, "_vm_tmp_i64_a");
                                    // rs2 is unsigned
                                    asm.i32array_set("_vm_bcopy_i32", &const0, &s2);
                                    asm.i32array_set("_vm_bcopy_i32", &const1, &const0);
                                    asm.bcopy(
                                        "_vm_bcopy_i32",
                                        &const0,
                                        "_vm_bcopy_i64",
                                        &const0,
                                        &asm.ensure_i32(8),
                                    );
                                    asm.i64array_get("_vm_bcopy_i64", &const0, "_vm_tmp_i64_b");
                                    // actual operation
                                    asm.i64_mul("_vm_tmp_i64_a", "_vm_tmp_i64_b", "_vm_tmp_i64_a");
                                }
                                _ => {
                                    // NYI?
                                    uasm_stop!(asm.asm());
                                }
                            }
                            // all codepaths pull the result this way
                            gen_i64_bitcast_i32(&asm, "_vm_tmp_i64_a", rd, true);
                        }

                        // unrecognized
                        _ => {
                            uasm_stop!(asm.asm());
                            fallthrough_ok = true;
                        }
                    }
                }
            }
            // Legacy syscalls. We'll keep this around even if we aren't using it.
            Kip32FIC::I(Sci32Instr::ECALL) => {
                // This is for the sake of the ECALL handler.
                // This allows it to return back to normal program execution just using _vm_indirect_jump.
                asm.u32_fromi32(asm.ensure_i32(istr.jump as i32), "vm_indirect_jump_target");
                uasm_op!(asm.asm(), JUMP, "_ecall");
                fallthrough_ok = true;
            }
            Kip32FIC::NametableSyscall(name) => {
                asm_syscall(&asm, &img, &name, istr.jump);
            }
            // unrecognized, break, etc.
            _ => {
                uasm_stop!(asm.asm());
                fallthrough_ok = true;
            }
        }

        if !fallthrough_ok {
            asm.jump_ui(resolve_jump(&asm, &img, istr.jump));
        }
    }

    // -- final stage --

    let result_text = if udonjson {
        udonprogram_emit_udonjson(&asm.asm.borrow())
            .expect("emit of udonjson should work")
            .dump()
    } else {
        let uasm_writer = UASMWriter::default();

        let res = udonprogram_emit_uasm(&asm.asm.borrow(), &uasm_writer);
        if emit_check {
            res.expect("emit of UASM should work");
        } else if let Err(err) = res {
            // be loud about it
            eprintln!("{}", err);
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
