use crate::{
    UdonAccess, UdonAnnotatedRawProgram, UdonGameObjectComponentHeapReference, UdonHeapSlot,
    UdonHeapValue, UdonInt, UdonProgram, UdonRawSymbol, UdonSymbol, UdonUnityObject,
};
use kudoninfo::{UdonTypeRef, udontyperef, udontyperef_from_odin};
use kudonodin::*;
use std::collections::BTreeSet;

/// Simplifies heap values that are unnecessarily complicated.
pub fn udonheapvalue_simplify(heap: UdonHeapValue) -> UdonHeapValue {
    match heap {
        UdonHeapValue::OdinASTInsert(insert) => {
            if let Ok(runtimetype) = OdinSTDeserializable::deserialize_insert(&insert.file) {
                let runtimetype: OdinSTRuntimeType = runtimetype;
                return UdonHeapValue::RType(runtimetype.0);
            }
            match insert.file.root {
                kudonodin::OdinASTValue::Primitive(prim) => return UdonHeapValue::P(prim),
                kudonodin::OdinASTValue::InternalRef(ir) => {
                    if let Some(ir_res) = insert.file.refs.get(&ir) {
                        if let Ok(extd) = ir_res.unwrap_fixed_type(
                            "VRC.Udon.VM.UdonVM+CachedUdonExternDelegate, VRC.Udon.VM",
                            0,
                        ) {
                            if let Ok(udonext) =
                                odinst_get_field(&insert.file.refs, extd, "externSignature")
                            {
                                return UdonHeapValue::P(OdinPrimitive::String(udonext));
                            }
                        }
                    }
                    UdonHeapValue::OdinASTInsert(insert)
                }
                _ => UdonHeapValue::OdinASTInsert(insert),
            }
        }
        _ => heap,
    }
}

pub fn udonrawsymbol_to_udonsymbol(sym: &UdonRawSymbol, exported: &BTreeSet<String>) -> UdonSymbol {
    let mut udon_type: Option<UdonTypeRef> = None;
    if let Some(ty) = &sym.ty {
        udon_type = Some(udontyperef_from_odin(ty));
    }
    UdonSymbol {
        name: sym.name.clone(),
        udon_type,
        address: UdonInt::I(sym.address as i64),
        mode: if exported.contains(&sym.name) {
            UdonAccess::Public
        } else {
            UdonAccess::Symbol
        },
    }
}

/// Disassembles an UdonAnnotatedRawProgram.
/// This is for debugging only and may not be 100% accurate. Will fail silently or with in-band noise.
pub fn udonannotatedrawprogram_disassemble(raw_program: &UdonAnnotatedRawProgram) -> UdonProgram {
    let mut program = UdonProgram::default();
    // code
    for v in &raw_program.program.bytecode {
        program.code.push(UdonInt::I(*v as i64));
    }
    // heap
    let mut valid_heap_index_count = 0;
    for v in &raw_program.program.heap.0 {
        if let Some((odin_type, odin_value)) = v {
            // remap to 'localized' unity object list
            let mut local_unity_objs: Vec<UdonUnityObject> = Vec::new();
            let mut global_to_local: std::collections::HashMap<i32, i32> = Default::default();
            let mut odin_ast_insert = odin_value.clone();
            // not doing internal reference transformation, so can safely ignore this
            _ = odin_ast_insert.root.remap_refs(
                None,
                &mut Some(&mut |global_idx| {
                    if let Some(local_idx) = global_to_local.get(&global_idx) {
                        *local_idx
                    } else {
                        // need to add to mapping
                        let local_idx = local_unity_objs.len() as i32;
                        global_to_local.insert(global_idx, local_idx);
                        if global_idx < 0 || (global_idx as usize) >= raw_program.unity_obj.len() {
                            local_unity_objs.push(UdonUnityObject::Ref(
                                format!("INVALID_UO_{}", global_idx),
                                0,
                            ));
                        } else {
                            local_unity_objs
                                .push(raw_program.unity_obj[global_idx as usize].clone());
                        }
                        global_idx
                    }
                }),
            );
            let mut udontyperef = udontyperef_from_odin(odin_type);
            // check for 'this''
            let this_replacement = udontyperef!(VRCUdonCommonUdonGameObjectComponentHeapReference);
            if this_replacement.name.as_str().eq(udontyperef.name.as_str()) {
                // try to change over the type reference if we can
                if let Ok(newtype) =
                    UdonGameObjectComponentHeapReference::deserialize_insert(&odin_ast_insert)
                {
                    udontyperef = udontyperef_from_odin(&newtype.0);
                }
                program
                    .data
                    .push(UdonHeapSlot(udontyperef, UdonHeapValue::This));
            } else {
                // now attach value
                program.data.push(UdonHeapSlot(
                    udontyperef,
                    udonheapvalue_simplify(UdonHeapValue::OdinASTInsert(
                        crate::UdonOdinASTInsert {
                            file: odin_ast_insert,
                            unity_objects: local_unity_objs,
                        },
                    )),
                ));
            }
            valid_heap_index_count = program.data.len();
        } else {
            program.data.push(UdonHeapSlot(
                udontyperef!(SystemObject),
                UdonHeapValue::P(OdinPrimitive::Null),
            ));
        }
    }
    program.data.truncate(valid_heap_index_count);
    // code symbols
    let mut exported_code: BTreeSet<String> = Default::default();
    for v in &raw_program.program.entry_points.exported_symbols {
        exported_code.insert(v.clone());
    }
    for v in &raw_program.program.entry_points.symbols {
        program
            .code_syms
            .push(udonrawsymbol_to_udonsymbol(v, &exported_code));
    }
    // data symbols
    let mut exported_data: BTreeSet<String> = Default::default();
    for v in &raw_program.program.symbol_table.exported_symbols {
        exported_data.insert(v.clone());
    }
    for v in &raw_program.program.symbol_table.symbols {
        program
            .data_syms
            .push(udonrawsymbol_to_udonsymbol(v, &exported_data));
    }
    // sync metadata
    for v in &raw_program.program.sync_metadata_table.0 {
        for v2 in &v.1 {
            program
                .sync_metadata
                .push((v.0.clone(), v2.0.clone(), v2.1));
        }
    }
    // other metadata
    program.update_order = UdonInt::I(raw_program.program.update_order as i64);
    program.network_call_metadata = raw_program.network_call_metadata.clone();
    program
}
