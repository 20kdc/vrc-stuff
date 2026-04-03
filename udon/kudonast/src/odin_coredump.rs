//! This module is for deserializing 'Udon coredumps' from kvtools.
//! The heap is presently by-value; this'll probably get resolved when needed.
//! It seems the Udon VM takes its heap from the IUdonProgram as-is, so the heap inside the dump-provided program will match the heap in the dump.
//! This makes initial heap values unrecoverable. This is probably fine.

use crate::{UdonRawHeap, UdonRawProgram};
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
