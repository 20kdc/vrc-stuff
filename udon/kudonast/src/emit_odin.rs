use crate::*;
use json::JsonValue;
use std::io::Write;

pub fn udonheapval_emit_odin_astinsert(
    val: &UdonOdinASTInsert,
    builder: &mut OdinASTBuilder,
    unity_obj: &mut Vec<UdonUnityObject>,
) -> Result<OdinASTValue, String> {
    let mut incres = builder
        .include(val.file.clone())
        .map_err(|v| format!("OdinASTInsert error: {:?}", v))?;
    for v in &val.unity_objects {
        unity_obj.push(v.clone());
    }
    builder.next_extid += val.unity_objects.len() as i32;
    if let Some(OdinASTEntry::Value(_, value)) = incres.0.pop() {
        Ok(value)
    } else {
        Err("OdinASTInsert did not resolve to a single Value entry".to_string())
    }
}

const UHV_THIS_REPLACEMENT: &'static UdonTypeRef =
    &UdonTypeRef::C(&kudoninfo::udon_types::VRCUdonCommonUdonGameObjectComponentHeapReference);

/// Translates UdonHeapValue to OdinASTValue.
pub fn udonheapval_emit_odin<'src>(
    val: &'src UdonHeapSlot,
    symtab: &BTreeMap<String, i64>,
    builder: &mut OdinASTBuilder,
    unity_obj: &mut Vec<UdonUnityObject>,
) -> Result<(&'src UdonTypeRef, OdinASTValue), String> {
    match &val.1 {
        UdonHeapValue::P(p) => Ok((&val.0, OdinASTValue::Primitive(p.clone()))),
        UdonHeapValue::PrimitiveArray(ut, p) => {
            let ref_id = builder.alloc_refid();
            builder.file.refs.insert(
                ref_id,
                OdinASTStruct(
                    Some(ut.odin_name.to_string()),
                    vec![OdinASTEntry::PrimitiveArray(p.clone())],
                ),
            );
            Ok((&val.0, OdinASTValue::InternalRef(ref_id)))
        }
        UdonHeapValue::OdinASTStruct(p) => Ok((&val.0, OdinASTValue::Struct(p.clone()))),
        UdonHeapValue::I(it, v) => Ok((
            &val.0,
            OdinASTValue::Primitive(OdinPrimitive::compose_int(*it, v.resolve(symtab)?)),
        )),
        UdonHeapValue::RType(ty) => {
            Ok((&val.0, OdinASTValue::InternalRef(builder.runtime_type(&ty))))
        }
        UdonHeapValue::This => {
            let ref_id = builder.alloc_refid();
            let rt = builder.runtime_type(&val.0.odin_name);
            builder.file.refs.insert(
                ref_id,
                OdinASTStruct(
                    Some(UHV_THIS_REPLACEMENT.odin_name.to_string()),
                    vec![OdinASTEntry::uval(OdinASTValue::InternalRef(rt))],
                ),
            );
            Ok((UHV_THIS_REPLACEMENT, OdinASTValue::InternalRef(ref_id)))
        }
        UdonHeapValue::OdinASTInsert(insert) => {
            udonheapval_emit_odin_astinsert(insert, builder, unity_obj).map(|v| (&val.0, v))
        }
    }
}

/// Builds the Udon Heap.
pub fn udonheap_emit_odin(
    min_heap_capacity: Option<u32>,
    heap: &[UdonHeapSlot],
    symtab: &BTreeMap<String, i64>,
    builder: &mut OdinASTBuilder,
    unity_obj: &mut Vec<UdonUnityObject>,
) -> Result<OdinASTStruct, String> {
    let heap_inner_ref_id = builder.alloc_refid();

    let mut true_heap_vec: Vec<OdinASTEntry> = Vec::new();

    // Let me just make it totally clear for you.
    // This code? Is nonsense.
    // There's no comment here on how it works because there isn't a "works" here.
    // It just builds a complicated asinine structure made up of layers of overlapping cruft.
    // This is not a place of honor.

    for (k, v) in heap.iter().enumerate() {
        let strongbox_ref_id = builder.alloc_refid();
        let (udon_type, emitted_val) = udonheapval_emit_odin(&v, symtab, builder, unity_obj)?;
        builder.file.refs.insert(
            strongbox_ref_id,
            OdinASTStruct(
                Some(format!(
                    "System.Runtime.CompilerServices.StrongBox`1[[{}]], System.Core",
                    udon_type.odin_name
                )),
                vec![OdinASTEntry::nval("Value", emitted_val)],
            ),
        );
        let runtime_type_ref_id = builder.runtime_type(&udon_type.odin_name);
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

    builder
        .file
        .refs
        .insert(heap_inner_ref_id, heap_inner_struct);

    let min_heap_capacity = min_heap_capacity.unwrap_or(1);
    let heap_size = if (heap.len() as u32) < min_heap_capacity {
        min_heap_capacity
    } else {
        heap.len() as u32
    };

    Ok(OdinASTStruct(
        Some("VRC.Udon.Common.UdonHeap, VRC.Udon.Common".to_string()),
        vec![OdinASTEntry::Array(
            2,
            vec![
                OdinASTEntry::nval("type", "System.UInt32, mscorlib"),
                OdinASTEntry::nval("HeapCapacity", OdinPrimitive::UInt(heap_size)),
                OdinASTEntry::nval(
                    "type",
                    "System.Collections.Generic.List`1[[System.ValueTuple`3[[System.UInt32, mscorlib],[System.Runtime.CompilerServices.IStrongBox, System.Core],[System.Type, mscorlib]], mscorlib]], mscorlib",
                ),
                OdinASTEntry::nval("HeapDump", OdinASTValue::InternalRef(heap_inner_ref_id)),
            ],
        )],
    ))
}

/// Builds the UdonSymbolTable.
pub fn udonsymboltable_emit_odin(
    table: &[UdonSymbol],
    symtab: &BTreeMap<String, i64>,
    builder: &mut OdinASTBuilder,
) -> Result<OdinASTStruct, String> {
    let symbol_list_ref_id = builder.alloc_refid();

    let mut symbol_vec = Vec::new();

    // These seem to either be sorted or randomized - to make the docExample test work, we'll try to get them sorted correctly
    let mut export_set = Vec::new();

    for v in table {
        let symref = builder.alloc_refid();
        let address = v.address.resolve(symtab)?;
        let mut typetype = OdinASTEntry::nval("type", "System.Object, mscorlib");
        let mut typeval = OdinASTEntry::nval("Type", OdinPrimitive::Null);
        if let Some(ty) = &v.udon_type {
            typetype = OdinASTEntry::nval("type", "System.RuntimeType, mscorlib");
            typeval = OdinASTEntry::nval(
                "Type",
                OdinASTValue::InternalRef(builder.runtime_type(&ty.odin_name)),
            );
        }
        builder.file.refs.insert(
            symref,
            OdinASTStruct(
                Some("VRC.Udon.Common.UdonSymbol, VRC.Udon.Common".to_string()),
                vec![OdinASTEntry::Array(
                    3,
                    vec![
                        OdinASTEntry::nval("type", "System.String, mscorlib"),
                        OdinASTEntry::nval("Name", v.name.as_str()),
                        typetype,
                        typeval,
                        OdinASTEntry::nval("type", "System.UInt32, mscorlib"),
                        OdinASTEntry::nval(
                            "Address",
                            OdinASTValue::Primitive(OdinPrimitive::UInt(address as u32)),
                        ),
                    ],
                )],
            ),
        );
        symbol_vec.push(OdinASTEntry::Value(None, OdinASTValue::InternalRef(symref)));
        if v.public {
            export_set.push((v.name.clone(), address));
        }
    }

    export_set.sort_by_key(|v| v.1);

    let mut export_vec = Vec::new();

    for v in export_set {
        export_vec.push(OdinASTEntry::uval(v.0.as_str()));
    }

    let symbol_list_struct = OdinASTStruct(Some("System.Collections.Generic.List`1[[VRC.Udon.Common.Interfaces.IUdonSymbol, VRC.Udon.Common]], mscorlib".to_string()), vec![
        OdinASTEntry::array(symbol_vec)
    ]);
    builder
        .file
        .refs
        .insert(symbol_list_ref_id, symbol_list_struct);

    let exports_ref_id = builder.alloc_refid();

    let export_list_struct = OdinASTStruct(
        Some("System.Collections.Generic.List`1[[System.String, mscorlib]], mscorlib".to_string()),
        vec![OdinASTEntry::array(export_vec)],
    );
    builder.file.refs.insert(exports_ref_id, export_list_struct);

    Ok(OdinASTStruct(
        Some("VRC.Udon.Common.UdonSymbolTable, VRC.Udon.Common".to_string()),
        vec![OdinASTEntry::Array(
            2,
            vec![
                OdinASTEntry::nval(
                    "type",
                    "System.Collections.Generic.List`1[[VRC.Udon.Common.Interfaces.IUdonSymbol, VRC.Udon.Common]], mscorlib",
                ),
                OdinASTEntry::nval("Symbols", OdinASTValue::InternalRef(symbol_list_ref_id)),
                OdinASTEntry::nval(
                    "type",
                    "System.Collections.Generic.List`1[[System.String, mscorlib]], mscorlib",
                ),
                OdinASTEntry::nval("ExportedSymbols", OdinASTValue::InternalRef(exports_ref_id)),
            ],
        )],
    ))
}

/// Builds the sync metadata.
pub fn udonsyncmetadata_emit_odin(
    table: &Vec<(String, String, u64)>,
    builder: &mut OdinASTBuilder,
) -> Result<OdinASTStruct, String> {
    let symbol_list_ref_id = builder.alloc_refid();

    let mut symbol_vec = Vec::new();

    // The basic idea is that we keep adding props to lists.
    // At the end (once all props are confirmed) we insert into the refs table.
    // (Situations like this are why the Odin AST refs table was built the way it was.)

    let mut symbol_prop_list_map: BTreeMap<String, (i32, Vec<OdinASTEntry>)> = BTreeMap::new();

    for (kname, kprop, v) in table {
        // Sort of a 'fill-in-the-blanks'.
        // We guarantee the prop_ref_id is in the list, and then we actually put the property into it.
        // Technically if tests weren't being so strict about object order this wouldn't matter.

        let prop_ref_id = match symbol_prop_list_map.get_mut(kname) {
            Some(xm) => {
                let prop_ref_id = builder.alloc_refid();
                xm.1.push(OdinASTEntry::uval(OdinASTValue::InternalRef(prop_ref_id)));
                prop_ref_id
            }
            None => {
                let usm_ref_id = builder.alloc_refid();
                let prop_list_ref_id = builder.alloc_refid();
                let prop_ref_id = builder.alloc_refid();

                builder.file.refs.insert(usm_ref_id, OdinASTStruct(Some("VRC.Udon.Common.UdonSyncMetadata, VRC.Udon.Common".to_string()), vec![
                    OdinASTEntry::Array(2, vec![
                        OdinASTEntry::nval("type", "System.String, mscorlib"),
                        OdinASTEntry::nval("Name", kname.as_str()),
                        OdinASTEntry::nval("type", "System.Collections.Generic.List`1[[VRC.Udon.Common.Interfaces.IUdonSyncProperty, VRC.Udon.Common]], mscorlib"),
                        OdinASTEntry::nval("Properties", OdinASTValue::InternalRef(prop_list_ref_id)),
                    ])
                ]));
                symbol_vec.push(OdinASTEntry::Value(
                    None,
                    OdinASTValue::InternalRef(usm_ref_id),
                ));

                symbol_prop_list_map.insert(
                    kname.clone(),
                    (
                        prop_list_ref_id,
                        vec![OdinASTEntry::uval(OdinASTValue::InternalRef(prop_ref_id))],
                    ),
                );

                prop_ref_id
            }
        };

        builder.file.refs.insert(prop_ref_id, OdinASTStruct(Some("VRC.Udon.Common.UdonSyncProperty, VRC.Udon.Common".to_string()), vec![
            OdinASTEntry::Array(2, vec![
                OdinASTEntry::nval("type", "System.String, mscorlib"),
                OdinASTEntry::nval("Name", kprop.as_str()),
                OdinASTEntry::nval("type", "VRC.Udon.Common.Interfaces.UdonSyncInterpolationMethod, VRC.Udon.Common"),
                OdinASTEntry::nval("InterpolationAlgorithm", OdinPrimitive::ULong(*v)),
            ])
        ]));
    }

    while let Some((_, v)) = symbol_prop_list_map.pop_first() {
        builder.file.refs.insert(v.0, OdinASTStruct(Some("System.Collections.Generic.List`1[[VRC.Udon.Common.Interfaces.IUdonSyncProperty, VRC.Udon.Common]], mscorlib".to_string()), vec![
            OdinASTEntry::array(v.1)
        ]));
    }

    let symbol_list_struct = OdinASTStruct(Some("System.Collections.Generic.List`1[[VRC.Udon.Common.Interfaces.IUdonSyncMetadata, VRC.Udon.Common]], mscorlib".to_string()), vec![
        OdinASTEntry::array(symbol_vec)
    ]);
    builder
        .file
        .refs
        .insert(symbol_list_ref_id, symbol_list_struct);

    Ok(OdinASTStruct(
        Some("VRC.Udon.Common.UdonSyncMetadataTable, VRC.Udon.Common".to_string()),
        vec![OdinASTEntry::Array(
            1,
            vec![
                OdinASTEntry::nval(
                    "type",
                    "System.Collections.Generic.List`1[[VRC.Udon.Common.Interfaces.IUdonSyncMetadata, VRC.Udon.Common]], mscorlib",
                ),
                OdinASTEntry::nval(
                    "SyncMetadata",
                    OdinASTValue::InternalRef(symbol_list_ref_id),
                ),
            ],
        )],
    ))
}

pub fn udonprogram_emit_odin(
    program: &UdonProgram,
) -> Result<(OdinASTFile, Vec<UdonUnityObject>), String> {
    let mut builder = OdinASTBuilder::default();
    let mut unity_obj = Vec::new();

    // Allocate these first. They're always consecutive, and it's convenient.
    let udonprogram_ref_id = builder.alloc_refid();

    let bytecode_ref_id = builder.alloc_refid();

    let mut bytecode: Vec<u8> = Vec::with_capacity(program.code.len() * 4);

    for v in &program.code {
        let val = v.resolve(&program.internal_syms)?;
        bytecode.extend((val as u32).to_be_bytes().iter());
    }

    let bytecode_struct = OdinASTStruct(
        Some("System.Byte[], mscorlib".to_string()),
        vec![OdinASTEntry::PrimitiveArray(OdinPrimitiveArray::U8(
            bytecode,
        ))],
    );
    builder.file.refs.insert(bytecode_ref_id, bytecode_struct);

    let heap_ref_id = builder.alloc_refid();

    let heap_struct = udonheap_emit_odin(
        program.min_heap_capacity,
        &program.data,
        &program.internal_syms,
        &mut builder,
        &mut unity_obj,
    )?;
    builder.file.refs.insert(heap_ref_id, heap_struct);

    let entrypoints_ref_id = builder.alloc_refid();
    let entrypoints_struct =
        udonsymboltable_emit_odin(&program.code_syms, &program.internal_syms, &mut builder)?;
    builder
        .file
        .refs
        .insert(entrypoints_ref_id, entrypoints_struct);

    let symboltable_ref_id = builder.alloc_refid();
    let symboltable_struct =
        udonsymboltable_emit_odin(&program.data_syms, &program.internal_syms, &mut builder)?;
    builder
        .file
        .refs
        .insert(symboltable_ref_id, symboltable_struct);

    let syncmetadata_ref_id = builder.alloc_refid();
    let syncmetadata_struct = udonsyncmetadata_emit_odin(&program.sync_metadata, &mut builder)?;
    builder
        .file
        .refs
        .insert(syncmetadata_ref_id, syncmetadata_struct);

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

    builder
        .file
        .root
        .push(OdinASTEntry::uval(OdinASTValue::InternalRef(
            udonprogram_ref_id,
        )));

    Ok((builder.file, unity_obj))
}

pub fn udonprogram_emit_udonjson(program: &UdonProgram) -> Result<JsonValue, String> {
    let (stage1_file, unityobjs) = udonprogram_emit_odin(program)?;
    let udon_binary = OdinEntry::write_all_to_bytes(&stage1_file.to_entry_vec());
    let final_binary = Vec::new();

    let mut gz_encoder =
        flate2::write::GzEncoder::new(final_binary, flate2::Compression::default());
    gz_encoder
        .write_all(&udon_binary)
        .map_err(|v| format!("{:?}", v))?;
    let encoded = gz_encoder.finish().map_err(|v| format!("{:?}", v))?;

    let serialized_program_compressed_bytes =
        JsonValue::Array(encoded.iter().map(|v| (*v as f64).into()).collect());

    let program_unity_engine_objects = JsonValue::Array(
        unityobjs
            .iter()
            .map(|v| match v {
                UdonUnityObject::Ref(guid, file_id) => {
                    let mut res = JsonValue::new_object();
                    res["guid"] = guid.clone().into();
                    res["fileID"] = (*file_id as f64).into();
                    res
                }
            })
            .collect(),
    );

    let mut network_calling_entrypoint_metadata = Vec::new();
    for v in &program.network_call_metadata {
        let mut res = JsonValue::new_object();
        res["_name"] = v.name.clone().into();
        let mut parameters_arr = Vec::new();
        for p in &v.parameters {
            let mut res = JsonValue::new_object();
            res["_name"] = p.0.clone().into();
            res["_type"] =
                p.1.sync_type
                    .ok_or_else(|| {
                        format!(
                            "Network RPC {} parameter {} has no sync type",
                            &v.name, &p.0
                        )
                    })?
                    .into();
            parameters_arr.push(res);
        }
        res["_parameters"] = JsonValue::Array(parameters_arr);
        res["_maxEventsPerSecond"] = v.max_events_per_second.into();
        network_calling_entrypoint_metadata.push(res);
    }

    let mut monobehaviour = JsonValue::new_object();
    monobehaviour["serializedProgramCompressedBytes"] = serialized_program_compressed_bytes;
    monobehaviour["programUnityEngineObjects"] = program_unity_engine_objects;
    monobehaviour["networkCallingEntrypointMetadata"] =
        JsonValue::Array(network_calling_entrypoint_metadata);

    let mut outer_object = JsonValue::new_object();
    outer_object["MonoBehaviour"] = monobehaviour;
    Ok(outer_object)
}
