//! This contains a set of 'raw' Odin reading types.
//! This doesn't have the abstraction that kudonast usually has.
//! This is useful for ingesting Udon coredumps.

use crate::{UdonRawHeap, UdonRawProgram};
/// Raw Udon heap.
use kudonodin::*;
use serde::{Deserialize, Serialize};

/// Rust equivalent of KDCVRCTools.KDCUdonCoreDump.
#[derive(Clone, Debug, Serialize, Deserialize, Default)]
pub struct UdonCoreDump {
    pub program: UdonRawProgram,
    pub error_pc: u32,
    pub heap: UdonRawHeap,
    pub stack: Vec<u32>,
}

impl OdinSTDeserializableRefType for UdonCoreDump {
    fn deserialize(src: &OdinASTRefMap, val: &OdinASTStruct) -> Result<Self, String> {
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
