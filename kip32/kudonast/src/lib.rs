//! `kudonast`'s job is to provide an 'Udon AST'.
//! This AST is not designed for complete round-trip 1:1 conversion (for that, maybe consider `kudonodin`).
//! This AST is designed to be easily assembled to, but decently flexible.

use kudoninfo::{UdonSpaciality, UdonType, UdonOpcode};
use kudonodin::*;
use serde::{Serialize, Deserialize};
use std::collections::BTreeMap;

// -- part 1: linker value resolver --

/// Resolved internal symbol.
pub type UdonResolvedInternalSym = (UdonSpaciality, i64);

/// Calculatable integer in the Udon AST.
/// In a linked program, all UdonInt instances are replaced with just the one kind.
#[derive(Clone, Debug, Serialize, Deserialize)]
pub enum UdonInt {
    /// Constant integer.
    I(i64),
    /// Udon opcode.
    /// This is useful because you can write Op([kudoninfo::opcodes::COPY])
    Op(&'static UdonOpcode),
    /// Internal symbol.
    Sym(String),
    /// Add X + Y.
    Add(Box<UdonInt>, Box<UdonInt>),
    /// Subtract X - Y.
    Sub(Box<UdonInt>, Box<UdonInt>),
    /// Multiply X * Y.
    Mul(Box<UdonInt>, Box<UdonInt>),
}

impl Default for UdonInt {
    fn default() -> Self {
        Self::I(0)
    }
}

impl UdonInt {
    /// Resolves to a constant.
    pub fn resolve(&self, symtab: &BTreeMap<String, UdonResolvedInternalSym>) -> Result<i64, String> {
        match self {
            Self::I(v) => Ok(*v),
            Self::Op(op) => Ok(op.opcode as i64),
            Self::Sym(sym) => match symtab.get(sym) {
                None => Err(format!("missing internal symbol {}", sym)),
                Some(v) => Ok(v.1)
            },
            Self::Add(a, b) => Ok(a.resolve(symtab)?.wrapping_add(b.resolve(symtab)?)),
            Self::Sub(a, b) => Ok(a.resolve(symtab)?.wrapping_sub(b.resolve(symtab)?)),
            Self::Mul(a, b) => Ok(a.resolve(symtab)?.wrapping_mul(b.resolve(symtab)?)),
        }
    }
}

// -- part 2: The Rest --

/// Written Udon symbol content.
#[derive(Clone, Debug, Serialize, Deserialize)]
pub struct UdonSymbol {
    pub name: String,
    pub udon_type: Option<UdonType>,
    pub address: UdonInt,
    pub public: bool,
}

/// Reference to a Unity object.
#[derive(Clone, Debug, Serialize, Deserialize)]
pub enum UdonUnityObject {
    Ref(String, i64),
}

/// Used to insert arbitrary data.
#[derive(Clone, Debug, Serialize, Deserialize)]
pub struct UdonOdinASTInsert {
    /// The OdinASTFile representing this value.
    /// The root entries must solely consist of a single Value entry.
    /// This Value entry will be patched to have the correct name if necessary.
    pub file: OdinASTFile,
    pub unity_objects: Vec<UdonUnityObject>
}

/// Udon heap value specification.
/// More-or-less directly translates to Odin AST data.
#[derive(Clone, Debug, Serialize, Deserialize)]
pub enum UdonHeapValue {
    /// Odin primitve.
    P(OdinPrimitive),
    /// Calculated integer.
    I(OdinIntType, UdonInt),
    /// .NET type.
    /// String is used because Udon Assembly can't represent this anyway.
    RType(String),
    /// UdonGameObjectComponentHeapReference is used for Const This.
    UdonGameObjectComponentHeapReference(UdonType),
    /// Inserted Odin AST.
    OdinASTInsert(UdonOdinASTInsert)
}

/// Udon heap slot.
/// Note that UdonGameObjectComponentHeapReference 'supplants' the type in an unusual way.
#[derive(Clone, Debug, Serialize, Deserialize)]
pub struct UdonHeapSlot(pub UdonType, pub UdonHeapValue);

/// Network call metadata.
#[derive(Clone, Debug, Serialize, Deserialize)]
pub struct UdonNetworkCallMetadata {
    pub name: String,
    pub max_events_per_second: i32,
    pub parameters: Vec<UdonNetworkCallParameter>
}

/// Network call parameter.
#[derive(Clone, Debug, Serialize, Deserialize)]
pub struct UdonNetworkCallParameter {
    pub name: String,
    /// Sync type (see [kudoninfo::UDON_SYNCTYPES])
    pub sync_type: i32
}

/// This does not match neatly with IUdonProgram; it really translates best to a `.udonjson` file.
#[derive(Clone, Debug, Serialize, Deserialize, Default)]
pub struct UdonProgram {
    /// Instructions; the actual bytecode.
    pub code: Vec<UdonInt>,
    /// Minimum heap capacity. This is used to emulate.
    /// Defaults to 1.
    pub min_heap_capacity: Option<u32>,
    /// Data
    pub data: Vec<UdonHeapSlot>,
    /// This is a reasonably literal transliteration into this assembler's world.
    pub code_syms: Vec<UdonSymbol>,
    /// This is similar.
    pub data_syms: Vec<UdonSymbol>,
    /// This mapping is hacky but should work.
    pub sync_metadata: Vec<(String, u64)>,
    /// Behold, network call metadata.
    pub network_call_metadata: Option<Vec<UdonNetworkCallMetadata>>,
    /// 'Internal' symbol table.
    /// This symbol table is not written out, only used in UdonInt calculations.
    /// When writing to Udon Assembly, the strings here are _not used._
    pub internal_syms: BTreeMap<String, UdonResolvedInternalSym>,
    /// Update order.
    pub update_order: UdonInt
}

mod emit_odin;
pub use emit_odin::*;

#[cfg(test)]
mod tests;
