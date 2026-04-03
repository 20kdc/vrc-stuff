use crate::*;
use serde::{Deserialize, Serialize};

/// Raw Udon heap.
/// Assumes some Odin AST 'elsewhere' for references.
/// The OdinASTInsert here is used to try and guarantee we write in canonical order.
/// Note that OdinASTInserts can be invalid. Run ref compaction on them to detect errors before you serialize, or panics may result.
#[derive(Clone, Debug, Serialize, Deserialize, Default)]
pub struct UdonRawHeap(pub Vec<Option<(String, OdinASTInsert)>>);

struct UdonRawHeapTriple(u32, String, OdinASTInsert);

impl OdinSTDeserializable for UdonRawHeapTriple {
    fn deserialize(src: &OdinASTRefMap, val: &OdinASTValue) -> Result<Self, String> {
        if let OdinASTValue::Struct(s) = val {
            let idx_v: u32 = odinst_get_field(src, &s.1, "Item1")?;
            let val_v: OdinSTStrongBox<OdinASTValue> = odinst_get_field(src, &s.1, "Item2")?;
            let type_v: OdinSTRuntimeType = odinst_get_field(src, &s.1, "Item3")?;
            Ok(UdonRawHeapTriple(
                idx_v,
                type_v.0,
                OdinASTInsert::extract(src, val_v.1),
            ))
        } else {
            Err("UdonRawHeapTriple should be struct".to_string())
        }
    }
}

impl OdinSTSerializable for UdonRawHeapTriple {
    fn serialize(&self, builder: &mut OdinASTBuilder) -> OdinASTValue {
        // carefully fix the order, mmm
        let strongbox_refid = builder.alloc_refid();
        let strongbox_struct = OdinSTSerializableRefType::serialize(
            &OdinSTStrongBox(self.1.clone(), builder.include_insert(self.2.clone(), 0).expect("please do ref compact when setting up UdonRawHeapValue inserts to check for errors").0),
            builder,
        );
        builder.file.refs.insert(strongbox_refid, strongbox_struct);
        let strongbox = OdinASTValue::InternalRef(strongbox_refid);
        let runtime_type: OdinASTValue =
            OdinSTSerializable::serialize(&OdinSTRuntimeType(self.1.clone()), builder);
        OdinASTValue::Struct(OdinASTStruct(Some("System.ValueTuple`3[[System.UInt32, mscorlib],[System.Runtime.CompilerServices.IStrongBox, System.Core],[System.Type, mscorlib]], mscorlib".to_string()), vec![
            OdinASTEntry::nval("Item1", OdinPrimitive::UInt(self.0)),
            OdinASTEntry::nval("Item2", strongbox),
            OdinASTEntry::nval("Item3", runtime_type)
        ]))
    }
}

impl OdinSTDeserializableRefType for UdonRawHeap {
    fn deserialize(src: &OdinASTRefMap, val: &OdinASTStruct) -> Result<Self, String> {
        let content = val.unwrap_iserializable()?;
        let heap_capacity: u32 = OdinSTDeserializable::deserialize(
            src,
            OdinASTEntry::get_value_by_name("HeapCapacity", content)?,
        )?;
        let mut vec = Vec::new();
        for _ in 0..heap_capacity {
            vec.push(None);
        }
        if let Ok(OdinASTValue::InternalRef(dump_idx)) =
            OdinASTEntry::get_value_by_name("HeapDump", content)
        {
            if let Some(dump_content_a) = src.get(dump_idx) {
                if let Some(OdinASTEntry::Array(_, dump_content)) = dump_content_a.1.get(0) {
                    for heap_slot in dump_content {
                        if let OdinASTEntry::Value(_, val) = heap_slot {
                            if let Ok(triple) = UdonRawHeapTriple::deserialize(src, val) {
                                if triple.0 < heap_capacity {
                                    vec[triple.0 as usize] = Some((triple.1, triple.2));
                                }
                            }
                        }
                    }
                }
            }
        }
        Ok(UdonRawHeap(vec))
    }
}

impl OdinSTSerializableRefType for UdonRawHeap {
    fn serialize(&self, builder: &mut OdinASTBuilder) -> OdinASTStruct {
        let heap_inner_ref_id = builder.alloc_refid();

        let mut true_heap_vec: Vec<OdinASTEntry> = Vec::new();
        for (k, v) in self.0.iter().enumerate() {
            if let Some(v) = v {
                let rht = UdonRawHeapTriple(k as u32, v.0.clone(), v.1.clone());
                true_heap_vec.push(OdinASTEntry::uval(OdinSTSerializable::serialize(
                    &rht, builder,
                )));
            }
        }
        // Finish encapsulation
        let heap_inner_struct = OdinASTStruct(Some("System.Collections.Generic.List`1[[System.ValueTuple`3[[System.UInt32, mscorlib],[System.Runtime.CompilerServices.IStrongBox, System.Core],[System.Type, mscorlib]], mscorlib]], mscorlib".to_string()), vec![
            OdinASTEntry::array(true_heap_vec)
        ]);

        builder
            .file
            .refs
            .insert(heap_inner_ref_id, heap_inner_struct);

        OdinASTStruct(
            Some("VRC.Udon.Common.UdonHeap, VRC.Udon.Common".to_string()),
            vec![OdinASTEntry::Array(
                2,
                vec![
                    OdinASTEntry::nval("type", "System.UInt32, mscorlib"),
                    OdinASTEntry::nval("HeapCapacity", OdinPrimitive::UInt(self.0.len() as u32)),
                    OdinASTEntry::nval(
                        "type",
                        "System.Collections.Generic.List`1[[System.ValueTuple`3[[System.UInt32, mscorlib],[System.Runtime.CompilerServices.IStrongBox, System.Core],[System.Type, mscorlib]], mscorlib]], mscorlib",
                    ),
                    OdinASTEntry::nval("HeapDump", OdinASTValue::InternalRef(heap_inner_ref_id)),
                ],
            )],
        )
    }
}

/// Raw Udon program.
/// This is now pretty much complete, but we can't use it to serialize because UdonRawHeap creates ordering issues by existing.
/// To fix this, we need a dedicated AST insert type and need to rework UdonRawHeap's value storage to use it.
#[derive(Clone, Debug, Serialize, Deserialize, Default)]
pub struct UdonRawProgram {
    pub bytecode: Vec<u32>,
    pub heap: UdonRawHeap,
    pub entry_points: UdonRawSymbolTable,
    pub symbol_table: UdonRawSymbolTable,
    pub sync_metadata_table: UdonRawSyncMetadataTable,
    pub update_order: i32,
}

impl OdinSTDeserializableRefType for UdonRawProgram {
    fn deserialize(src: &OdinASTRefMap, val: &OdinASTStruct) -> Result<Self, String> {
        let content = val.unwrap_fixed_type("VRC.Udon.Common.UdonProgram, VRC.Udon.Common", 0)?;
        let bytecode: Vec<u8> = odinst_get_field(src, content, "ByteCode")?;
        let mut bytecode_out: Vec<u32> = Vec::new();
        for i in 0..(bytecode.len() / 4) {
            let base = i * 4;
            let mut vifo: [u8; 4] = [0; 4];
            vifo.copy_from_slice(&bytecode[base..base + 4]);
            bytecode_out.push(u32::from_be_bytes(vifo));
        }
        let heap: UdonRawHeap = odinst_get_field(src, content, "Heap")?;
        let entry_points: UdonRawSymbolTable = odinst_get_field(src, content, "EntryPoints")?;
        let symbol_table: UdonRawSymbolTable = odinst_get_field(src, content, "SymbolTable")?;
        let sync_metadata_table: UdonRawSyncMetadataTable =
            odinst_get_field(src, content, "SyncMetadataTable")?;
        let update_order: i32 = odinst_get_field(src, content, "UpdateOrder")?;
        Ok(UdonRawProgram {
            bytecode: bytecode_out,
            heap,
            entry_points,
            symbol_table,
            sync_metadata_table,
            update_order,
        })
    }
}

impl OdinSTSerializableRefType for UdonRawProgram {
    fn serialize(&self, builder: &mut OdinASTBuilder) -> OdinASTStruct {
        let mut bytecode_data: Vec<u8> = Vec::with_capacity(self.bytecode.len() * 4);

        for val in &self.bytecode {
            bytecode_data.extend(val.to_be_bytes().iter());
        }

        let bytecode: OdinASTValue = OdinSTSerializable::serialize(&bytecode_data, builder);

        let heap: OdinASTValue = OdinSTSerializable::serialize(&self.heap, builder);

        let entrypoints: OdinASTValue = OdinSTSerializable::serialize(&self.entry_points, builder);

        let symboltable: OdinASTValue = OdinSTSerializable::serialize(&self.symbol_table, builder);

        let syncmetadata: OdinASTValue =
            OdinSTSerializable::serialize(&self.sync_metadata_table, builder);

        OdinASTStruct(
            Some("VRC.Udon.Common.UdonProgram, VRC.Udon.Common".to_string()),
            vec![
                OdinASTEntry::nval("InstructionSetIdentifier", "UDON"),
                OdinASTEntry::nval("InstructionSetVersion", OdinPrimitive::Int(1)),
                OdinASTEntry::nval("ByteCode", bytecode),
                OdinASTEntry::nval("Heap", heap),
                OdinASTEntry::nval("EntryPoints", entrypoints),
                OdinASTEntry::nval("SymbolTable", symboltable),
                OdinASTEntry::nval("SyncMetadataTable", syncmetadata),
                OdinASTEntry::nval("UpdateOrder", OdinPrimitive::Int(self.update_order)),
            ],
        )
    }
}
