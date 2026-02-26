use crate::*;
use std::cell::RefCell;
use std::collections::HashSet;

struct UASMTranslateCtx<'uw>(&'uw UASMWriter, RefCell<Vec<String>>);

impl UASMTranslateCtx<'_> {
    fn err_code(&self, detail: impl std::fmt::Display) {
        self.0.code(format!("# ERROR: {}", detail));
        self.1.borrow_mut().push(format!("Code: {}", detail));
    }
    fn err_data(&self, detail: impl std::fmt::Display) {
        self.0.data(format!("# ERROR: {}", detail));
        self.1.borrow_mut().push(format!("Data: {}", detail));
    }
}

/// Gets a single u32 of Udon code with integrated error handling.
fn get_code_or_error(program: &UdonProgram, at: usize, translate_ctx: &UASMTranslateCtx) -> u32 {
    if let Some(val) = program.code.get(at) {
        match val.resolve(&program.internal_syms) {
            Err(err) => {
                translate_ctx.err_code(err);
                0
            }
            Ok(v) => v as u32,
        }
    } else {
        translate_ctx.err_code("Instruction parser escaped the codebuffer. Was last heard joyfully shouting \"I'M FREE!\".");
        0
    }
}

fn codeptr_to_i64(codeptr: usize) -> i64 {
    (codeptr * 4) as i64
}

fn build_comment(comment: &str) -> String {
    let mut res = "\t# ".to_string();
    let mut first = true;
    for v in comment.split("\n") {
        if !first {
            res.push_str("\n\t# ");
        }
        first = false;
        res.push_str(v);
    }
    res
}

/// Translates [UdonProgram] into VRCSDK Udon Assembly.
/// This assembly is severely limited compared to kudonast.
pub fn udonprogram_emit_uasm(
    program: &UdonProgram,
    uasm_writer: &UASMWriter,
) -> Result<(), String> {
    let translate_ctx = UASMTranslateCtx(uasm_writer, RefCell::new(Vec::new()));

    // -- Code Early Pass --

    let mut valid_code_locations: HashSet<i64> = HashSet::new();

    let mut codeptr: usize = 0;

    while codeptr < program.code.len() {
        valid_code_locations.insert(codeptr_to_i64(codeptr));
        let opc = get_code_or_error(program, codeptr, &translate_ctx);
        codeptr += 1;
        let opcode = kudoninfo::sparse_table_get(kudoninfo::UDON_OPCODES, opc as usize);
        if let Some(opcode) = opcode {
            codeptr += opcode.parameters.len();
        }
    }
    valid_code_locations.insert(codeptr_to_i64(codeptr));

    // -- Symbols --

    // It's important that the code/data remap tables are always pointing to places we will put labels on.

    let mut code_remapped: BTreeMap<i64, UdonSymbol> = BTreeMap::new();
    for v in &program.code_syms {
        match v.address.resolve(&program.internal_syms) {
            Ok(address) => {
                if valid_code_locations.contains(&address) {
                    code_remapped.insert(address, v.clone());
                } else {
                    translate_ctx.err_code(format!(
                        "Code symbol {} resolved outside of code: {}",
                        v.name, address
                    ));
                }
            }
            Err(err) => {
                translate_ctx.err_code(format!("Code symbol {} did not resolve: {}", v.name, err));
            }
        }
    }

    let mut data_remapped: BTreeMap<i64, UdonSymbol> = BTreeMap::new();
    for v in &program.data_syms {
        match v.address.resolve(&program.internal_syms) {
            Ok(address) => {
                if address >= 0 && address < ((program.data.len()) as i64) {
                    data_remapped.insert(address, v.clone());
                } else {
                    translate_ctx.err_data(format!(
                        "Data symbol {} resolved outside of data: {}",
                        v.name, address
                    ));
                }
            }
            Err(err) => {
                translate_ctx.err_data(format!("Data symbol {} did not resolve: {}", v.name, err));
            }
        }
    }

    // -- Other Stuff --

    match program.update_order.resolve(&program.internal_syms) {
        Ok(res) => {
            if res != 0 {
                uasm_writer.code(format!(".update_order 0x{:08x}", res as u32));
                uasm_writer.code("");
            }
        }
        Err(err) => {
            translate_ctx.err_code(format!("update_order failed to resolve: {}", err));
            uasm_writer.code("");
        }
    }

    if !program.network_call_metadata.is_empty() {
        translate_ctx.err_code("Network call metadata is not supported.");
    }

    // -- Data --

    for (k, v) in program.data.iter().enumerate() {
        if let Some(comm) = program.data_comments.get(&k) {
            uasm_writer.data(build_comment(&comm));
        }
        let rmp = data_remapped
            .get(&(k as i64))
            .map(|v| v.clone())
            .unwrap_or_else(|| {
                let v = format!("__kudonastemituasm__{}", k);
                let sym = UdonSymbol {
                    name: v.clone(),
                    udon_type: None,
                    address: UdonInt::I(k as i64),
                    mode: UdonAccess::Symbol,
                };
                // retroactively add this
                data_remapped.insert(k as i64, sym.clone());
                sym
            });
        // originally added for UdonGameObjectComponentHeapReference swapping
        // then that was changed to be handled by AST
        let type_slot = &v.0;
        let value = match &v.1 {
            UdonHeapValue::P(prim) => {
                if let Some(int) = prim.decompose_int() {
                    format!("0x{:08x}", int.1 as u32)
                } else {
                    match prim {
                        OdinPrimitive::Null => "null".to_string(),
                        OdinPrimitive::Float(v) => {
                            format!("{}", v)
                        }
                        OdinPrimitive::Double(v) => {
                            format!("{}", v)
                        }
                        OdinPrimitive::String(s) => {
                            format!("\"{}\"", s)
                        }
                        _ => {
                            translate_ctx.err_data(format!(
                                "Data symbol {} contains untranslatable primitive {:?}",
                                rmp.name, prim
                            ));
                            "null".to_string()
                        }
                    }
                }
            }
            UdonHeapValue::I(_, i) => match i.resolve(&program.internal_syms) {
                Ok(v) => {
                    format!("0x{:08x}", v as u32)
                }
                Err(err) => {
                    translate_ctx
                        .err_data(format!("Data symbol {} failed resolve: {}", rmp.name, err));
                    "0".to_string()
                }
            },
            UdonHeapValue::This => "this".to_string(),
            _ => {
                translate_ctx.err_data(format!(
                    "Data symbol {} contains untranslatable value {:?}",
                    rmp.name, v.1
                ));
                "null".to_string()
            }
        };
        uasm_writer.declare_heap(
            &rmp.name,
            &type_slot.name,
            &value,
            rmp.mode == UdonAccess::Public,
        );
    }

    uasm_writer.data("");
    for v in &program.sync_metadata {
        // .sync variable->this
        // uasm_writer.data(format!("{}->{}", variable, this));
        let res = kudoninfo::sparse_table_get(kudoninfo::UDON_INTERPOLATIONS, v.2 as usize);
        let res = res.unwrap_or_else(|| {
            translate_ctx.err_data(format!(
                "sync metadata on {}->{} uses unknown interpolation {}",
                v.0, v.1, v.2
            ));
            "none"
        });
        if v.1.eq("this") {
            uasm_writer.data(format!("\t.sync {}, {}", v.0, res));
        } else {
            uasm_writer.data(format!("\t.sync {}->{}, {}", v.0, v.1, res));
        }
    }

    // -- Code --

    codeptr = 0;

    while codeptr < program.code.len() {
        if let Some(comm) = program.code_comments.get(&codeptr) {
            uasm_writer.code(build_comment(&comm));
        }
        if let Some(sym) = code_remapped.get(&codeptr_to_i64(codeptr)) {
            uasm_writer.code_label(&sym.name, sym.mode == UdonAccess::Public);
        }
        let opc = get_code_or_error(program, codeptr, &translate_ctx);
        codeptr += 1;
        let opcode = kudoninfo::sparse_table_get(kudoninfo::UDON_OPCODES, opc as usize);
        if let Some(opcode) = opcode {
            let mut info = format!("\t{}", opcode.name.to_string());
            for space in opcode.parameters {
                let pv = get_code_or_error(program, codeptr, &translate_ctx);
                codeptr += 1;
                let mut over: Option<&str> = None;
                let table = match space {
                    UdonSpaciality::Code => Some(&code_remapped),
                    UdonSpaciality::Data => Some(&data_remapped),
                    UdonSpaciality::DataExtern => Some(&data_remapped),
                    UdonSpaciality::Indistinct => None,
                    UdonSpaciality::Annotation => None,
                };
                if let Some(table) = table {
                    if let Some(sym) = table.get(&(pv as i64)) {
                        over = Some(&sym.name);
                    }
                }
                if let Some(over) = over {
                    info.push_str(&format!(", {}", over));
                } else {
                    info.push_str(&format!(", 0x{:08x}", pv));
                }
            }
            uasm_writer.code(info);
        } else {
            translate_ctx.err_code(format!("Invalid opcode {}", opc));
        }
    }
    if let Some(sym) = code_remapped.get(&codeptr_to_i64(codeptr)) {
        uasm_writer.code_label(&sym.name, sym.mode == UdonAccess::Public);
    }

    // -- Finale --

    if !translate_ctx.1.borrow().is_empty() {
        let mut total = String::new();
        let mut first = true;
        for v in translate_ctx.1.borrow().iter() {
            if !first {
                total.push_str("\n");
            } else {
                first = false;
            }
            total.push_str(&v);
        }
        Err(total)
    } else {
        Ok(())
    }
}
