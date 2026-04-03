use crate::*;
use serde::{Deserialize, Serialize};

/// Raw Udon heap.
/// Assumes some Odin AST 'elsewhere' for references.
/// This arrangement can't be written in 'canonical' order, since it's hiding a bunch of StrongBoxes.
#[derive(Clone, Debug, Serialize, Deserialize, Default)]
pub struct UdonRawHeap(pub Vec<Option<(String, OdinASTValue)>>);

struct UdonRawHeapTriple(u32, OdinASTValue, OdinSTRuntimeType);

impl OdinSTDeserializable for UdonRawHeapTriple {
    fn deserialize(src: &OdinASTFile, val: &OdinASTValue) -> Result<Self, String> {
        if let OdinASTValue::Struct(s) = val {
            let idx_v: u32 = odinst_get_field(src, &s.1, "Item1")?;
            let val_v: OdinSTStrongBox<OdinASTValue> = odinst_get_field(src, &s.1, "Item2")?;
            let type_v: OdinSTRuntimeType = odinst_get_field(src, &s.1, "Item3")?;
            Ok(UdonRawHeapTriple(idx_v, val_v.1, type_v))
        } else {
            Err("UdonRawHeapTriple should be struct".to_string())
        }
    }
}

impl OdinSTDeserializableRefType for UdonRawHeap {
    fn deserialize(src: &OdinASTFile, val: &OdinASTStruct) -> Result<Self, String> {
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
            if let Some(dump_content_a) = src.refs.get(dump_idx) {
                if let Some(OdinASTEntry::Array(_, dump_content)) = dump_content_a.1.get(0) {
                    for heap_slot in dump_content {
                        if let OdinASTEntry::Value(_, val) = heap_slot {
                            if let Ok(triple) = UdonRawHeapTriple::deserialize(src, val) {
                                if triple.0 < heap_capacity {
                                    vec[triple.0 as usize] = Some((triple.2.0, triple.1));
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
    fn deserialize(src: &OdinASTFile, val: &OdinASTStruct) -> Result<Self, String> {
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
