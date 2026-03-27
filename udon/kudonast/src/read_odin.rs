//! This contains a set of 'raw' Odin reading types.
//! This doesn't have the abstraction that kudonast usually has.
//! This is useful for ingesting Udon coredumps.

/// Raw Udon heap.
use kudonodin::*;
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

#[derive(Clone, Debug, Serialize, Deserialize, Default)]
pub struct UdonRawSymbol {
    pub name: String,
    pub ty: Option<String>,
    pub address: u32
}
impl OdinSTDeserializableRefType for UdonRawSymbol {
    fn deserialize(src: &OdinASTFile, val: &OdinASTStruct) -> Result<Self, String> {
        let content = val.unwrap_iserializable()?;
        let name: String = odinst_get_field(src, content, "Name")?;
        let ty: Option<OdinSTRuntimeType> = odinst_get_field(src, content, "Type").ok();
        let address: u32 = odinst_get_field(src, content, "Address")?;
        Ok(UdonRawSymbol {
            name,
            ty: ty.map(|v| v.0),
            address
        })
    }
}

#[derive(Clone, Debug, Serialize, Deserialize, Default)]
pub struct UdonRawSymbolTable {
    pub symbols: Vec<UdonRawSymbol>,
    pub exported_symbols: Vec<String>
}
impl OdinSTDeserializableRefType for UdonRawSymbolTable {
    fn deserialize(src: &OdinASTFile, val: &OdinASTStruct) -> Result<Self, String> {
        let content = val.unwrap_iserializable()?;
        let syms: OdinSTRefList<UdonRawSymbol> = odinst_get_field(src, content, "Symbols")?;
        let exported: OdinSTRefList<String> = odinst_get_field(src, content, "ExportedSymbols")?;
        Ok(Self {
            symbols: syms.0,
            exported_symbols: exported.0,
        })
    }
}

/// Raw Udon program.
/// Only contains what the coredump stuff cares about for now.
#[derive(Clone, Debug, Serialize, Deserialize, Default)]
pub struct UdonRawProgram {
    pub entry_points: UdonRawSymbolTable,
    pub symbol_table: UdonRawSymbolTable
}
impl OdinSTDeserializableRefType for UdonRawProgram {
    fn deserialize(src: &OdinASTFile, val: &OdinASTStruct) -> Result<Self, String> {
        let content = val.unwrap_fixed_type("VRC.Udon.Common.UdonProgram, VRC.Udon.Common", 0)?;
        let entry_points: UdonRawSymbolTable = odinst_get_field(src, content, "EntryPoints")?;
        let symbol_table: UdonRawSymbolTable = odinst_get_field(src, content, "SymbolTable")?;
        Ok(UdonRawProgram {
            entry_points,
            symbol_table,
        })
    }
}

/// Rust equivalent of KDCVRCTools.KDCUdonCoreDump.
#[derive(Clone, Debug, Serialize, Deserialize, Default)]
pub struct UdonCoreDump {
    pub program: UdonRawProgram,
    pub error_pc: u32,
    pub heap: UdonRawHeap,
    pub stack: Vec<u32>,
}

impl OdinSTDeserializableRefType for UdonCoreDump {
    fn deserialize(src: &OdinASTFile, val: &OdinASTStruct) -> Result<Self, String> {
        let content = val.unwrap_fixed_type("KDCVRCTools.KDCUdonCoreDump, KDCVRCTools", 0)?;
        // let program: UdonProgram = OdinASTEntry::get_value_by_name("program", content).and_then(|v| OdinSTDeserializable::deserialize(src, v));
        let program: UdonRawProgram = odinst_get_field(src, content, "program")?;
        let error_pc: u32 = odinst_get_field(src, content, "errorPC")?;
        let heap: UdonRawHeap = odinst_get_field(src, content, "heap")?;
        let stack: Vec<u32> = odinst_get_field(src, content, "stack")?;
        Ok(UdonCoreDump {
            program,
            error_pc,
            heap,
            stack,
        })
    }
}
