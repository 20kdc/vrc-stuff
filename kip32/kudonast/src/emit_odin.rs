use crate::*;
use kudonodin::*;

/// Translates UdonHeapValue to OdinASTValue.
pub fn udonheapval_emit_odin(val: &UdonHeapValue, symtab: &BTreeMap<String, UdonResolvedInternalSym>, builder: &mut OdinASTBuilder, _unity_obj: &mut Vec<UdonUnityObject>) -> Result<OdinASTValue, String> {
    match val {
        UdonHeapValue::Primitive(p) => Ok(OdinASTValue::Primitive(p.clone())),
        UdonHeapValue::CI32(v) => Ok(OdinASTValue::Primitive(OdinPrimitive::Int(v.resolve(symtab)? as i32))),
        UdonHeapValue::CU32(v) => Ok(OdinASTValue::Primitive(OdinPrimitive::UInt(v.resolve(symtab)? as u32))),
        UdonHeapValue::RuntimeType(ty) => Ok(OdinASTValue::InternalRef(builder.runtime_type(ty))),
        UdonHeapValue::OdinASTInsert(_insert) => {
            Err("OdinASTInsert NYI".to_string())
        }
    }
}

/// Builds the Udon Heap.
pub fn udonheap_emit_odin(heap: &[UdonHeapSlot], symtab: &BTreeMap<String, UdonResolvedInternalSym>, builder: &mut OdinASTBuilder, unity_obj: &mut Vec<UdonUnityObject>) -> Result<OdinASTStruct, String> {
    let heap_inner_ref_id = builder.alloc_refid();

    let mut true_heap_vec: Vec<OdinASTEntry> = Vec::new();

    // Let me just make it totally clear for you.
    // This code? Is nonsense.
    // There's no comment here on how it works because there isn't a "works" here.
    // It just builds a complicated asinine structure made up of layers of overlapping cruft.
    // This is not a place of honor.

    for (k, v) in heap.iter().enumerate() {
        let strongbox_ref_id = builder.alloc_refid();
        let emitted_val = udonheapval_emit_odin(&v.1, symtab, builder, unity_obj)?;
        builder.file.refs.insert(strongbox_ref_id, OdinASTStruct(Some(format!("System.Runtime.CompilerServices.StrongBox`1[[{}]], System.Core", v.0.odin_name)), vec![
            OdinASTEntry::nval("Value", emitted_val)
        ]));
        let runtime_type_ref_id = builder.runtime_type(&v.0.odin_name);
        true_heap_vec.push(OdinASTEntry::uval(OdinASTStruct(Some("System.ValueTuple`3[[System.UInt32, mscorlib],[System.Runtime.CompilerServices.IStrongBox, System.Core],[System.Type, mscorlib]], mscorlib".to_string()), vec![
            OdinASTEntry::nval("Item1", OdinPrimitive::UInt(k as u32)),
            OdinASTEntry::nval("Item2", OdinASTValue::InternalRef(strongbox_ref_id)),
            OdinASTEntry::nval("Item3", OdinASTValue::InternalRef(runtime_type_ref_id))
        ])));
    }

    // Finish encapsulation
    let heap_inner_struct = OdinASTStruct(Some("System.Collections.Generic.List`1[[System.ValueTuple`3[[System.UInt32, mscorlib],[System.Runtime.CompilerServices.IStrongBox, System.Core],[System.Type, mscorlib]], mscorlib]], mscorlib".to_string()), vec![
        OdinASTEntry::array(true_heap_vec)
    ]);

    builder.file.refs.insert(heap_inner_ref_id, heap_inner_struct);

    let heap_size = if heap.len() < 1 {
        1
    } else {
        heap.len() as u32
    };

    Ok(OdinASTStruct(Some("VRC.Udon.Common.UdonHeap, VRC.Udon.Common".to_string()), vec![
        OdinASTEntry::Array(2, vec![
            OdinASTEntry::nval("type", "System.UInt32, mscorlib"),
            OdinASTEntry::nval("HeapCapacity", OdinPrimitive::UInt(heap_size)),
            OdinASTEntry::nval("type", "System.Collections.Generic.List`1[[System.ValueTuple`3[[System.UInt32, mscorlib],[System.Runtime.CompilerServices.IStrongBox, System.Core],[System.Type, mscorlib]], mscorlib]], mscorlib"),
            OdinASTEntry::nval("HeapDump", OdinASTValue::InternalRef(heap_inner_ref_id)),
        ])
    ]))
}

/// Builds the UdonSymbolTable.
pub fn udonsymboltable_emit_odin(table: &BTreeMap<String, UdonSymbol>, symtab: &BTreeMap<String, UdonResolvedInternalSym>, builder: &mut OdinASTBuilder) -> Result<OdinASTStruct, String> {
    let symbol_list_ref_id = builder.alloc_refid();

    let mut symbol_vec = Vec::new();

    let mut export_vec = Vec::new();

    for (k, v) in table.iter() {
        let symref = builder.alloc_refid();
        let address = v.address.resolve(symtab)?;
        let mut typetype = OdinASTEntry::nval("type", "System.Object, mscorlib");
        let mut typeval = OdinASTEntry::nval("Type", OdinPrimitive::Null);
        if let Some(ty) = &v.udon_type {
            typetype = OdinASTEntry::nval("type", "System.RuntimeType, mscorlib");
            typeval = OdinASTEntry::nval("Type", OdinASTValue::InternalRef(builder.runtime_type(&ty.odin_name)));
        }
        builder.file.refs.insert(symref, OdinASTStruct(Some("VRC.Udon.Common.UdonSymbol, VRC.Udon.Common".to_string()), vec![
            OdinASTEntry::Array(3, vec![
                OdinASTEntry::nval("type", "System.String, mscorlib"),
                OdinASTEntry::nval("Name", k.as_str()),
                typetype,
                typeval,
                OdinASTEntry::nval("type", "System.UInt32, mscorlib"),
                OdinASTEntry::nval("Address", OdinASTValue::Primitive(OdinPrimitive::UInt(address))),
            ])
        ]));
        symbol_vec.push(OdinASTEntry::Value(None, OdinASTValue::InternalRef(symref)));
        if v.public {
            export_vec.push(OdinASTEntry::uval(k.as_str()));
        }
    }

    let symbol_list_struct = OdinASTStruct(Some("System.Collections.Generic.List`1[[VRC.Udon.Common.Interfaces.IUdonSymbol, VRC.Udon.Common]], mscorlib".to_string()), vec![
        OdinASTEntry::array(symbol_vec)
    ]);
    builder.file.refs.insert(symbol_list_ref_id, symbol_list_struct);

    let exports_ref_id = builder.alloc_refid();

    let export_list_struct = OdinASTStruct(Some("System.Collections.Generic.List`1[[System.String, mscorlib]], mscorlib".to_string()), vec![
        OdinASTEntry::array(export_vec)
    ]);
    builder.file.refs.insert(exports_ref_id, export_list_struct);

    Ok(OdinASTStruct(Some("VRC.Udon.Common.UdonSymbolTable, VRC.Udon.Common".to_string()), vec![
        OdinASTEntry::Array(2, vec![
            OdinASTEntry::nval("type", "System.Collections.Generic.List`1[[VRC.Udon.Common.Interfaces.IUdonSymbol, VRC.Udon.Common]], mscorlib"),
            OdinASTEntry::nval("Symbols", OdinASTValue::InternalRef(symbol_list_ref_id)),
            OdinASTEntry::nval("type", "System.Collections.Generic.List`1[[System.String, mscorlib]], mscorlib"),
            OdinASTEntry::nval("ExportedSymbols", OdinASTValue::InternalRef(exports_ref_id)),
        ])
    ]))
}

pub fn udonprogram_emit_odin(program: &UdonProgram) -> Result<(OdinASTFile, Vec<UdonUnityObject>), String> {
    let mut builder = OdinASTBuilder::default();
    let mut unity_obj = Vec::new();

    // Allocate these first. They're always consecutive, and it's convenient.
    let udonprogram_ref_id = builder.alloc_refid();
    let bytecode_ref_id = builder.alloc_refid();
    let heap_ref_id = builder.alloc_refid();

    let heap_struct = udonheap_emit_odin(&program.data, &program.internal_syms, &mut builder, &mut unity_obj)?;
    builder.file.refs.insert(heap_ref_id, heap_struct);

    let entrypoints_ref_id = builder.alloc_refid();
    let entrypoints_struct = udonsymboltable_emit_odin(&program.code_syms, &program.internal_syms, &mut builder)?;
    builder.file.refs.insert(entrypoints_ref_id, entrypoints_struct);

    let symboltable_ref_id = builder.alloc_refid();
    let symboltable_struct = udonsymboltable_emit_odin(&program.data_syms, &program.internal_syms, &mut builder)?;
    builder.file.refs.insert(symboltable_ref_id, symboltable_struct);

    let syncmetadata_ref_id = builder.alloc_refid();
    // TODO

    // Finish up.
    builder.file.refs.insert(udonprogram_ref_id, OdinASTStruct(Some("VRC.Udon.Common.UdonProgram, VRC.Udon.Common".to_string()), vec![
        OdinASTEntry::nval("InstructionSetIdentifier", "UDON"),
        OdinASTEntry::nval("InstructionSetVersion", OdinPrimitive::Int(1)),
        OdinASTEntry::nval("ByteCode", OdinASTValue::InternalRef(bytecode_ref_id)),
        OdinASTEntry::nval("Heap", OdinASTValue::InternalRef(heap_ref_id)),
        OdinASTEntry::nval("EntryPoints", OdinASTValue::InternalRef(entrypoints_ref_id)),
        OdinASTEntry::nval("SymbolTable", OdinASTValue::InternalRef(symboltable_ref_id)),
        OdinASTEntry::nval("SyncMetadataTable", OdinASTValue::InternalRef(syncmetadata_ref_id)),
        OdinASTEntry::nval("UpdateOrder", OdinPrimitive::Int(program.update_order.resolve(&program.internal_syms)? as i32)),
    ]));

    builder.file.root.push(OdinASTEntry::uval(OdinASTValue::InternalRef(udonprogram_ref_id)));

    Ok((builder.file, unity_obj))
}
