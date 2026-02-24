//! `kudonast`'s job is to provide an 'Udon AST'.
//! This AST is not designed for complete round-trip 1:1 conversion (for that, maybe consider `kudonodin`).
//! This AST is designed to be easily assembled to, but decently flexible.

use kudoninfo::{UdonOpcode, UdonSpaciality, UdonType};
use kudonodin::*;
use serde::{Deserialize, Serialize};
use std::collections::BTreeMap;

// -- part 1: linker value resolver --

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
    pub fn resolve(&self, symtab: &BTreeMap<String, i64>) -> Result<i64, String> {
        match self {
            Self::I(v) => Ok(*v),
            Self::Op(op) => Ok(op.opcode as i64),
            Self::Sym(sym) => match symtab.get(sym) {
                None => Err(format!("missing internal symbol {}", sym)),
                Some(v) => Ok(*v),
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
    /// _Ignored in UASM._
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
    pub unity_objects: Vec<UdonUnityObject>,
}

/// Udon heap value specification.
/// More-or-less directly translates to Odin AST data.
/// This struct is intended to be deliberately 'wide' (sadly making it harder to translate).
/// The idea is to make it convenient to specify any kind of value.
#[derive(Clone, Debug, Serialize, Deserialize)]
pub enum UdonHeapValue {
    /// Odin primitive.
    /// _Limited support in UASM -- all integers written as UInt._
    P(OdinPrimitive),
    /// Primitive array with the encompassing object auto-generated.
    /// This is the most convenient way to handle, i.e. [kudoninfo::udon_types::SystemByteArray].
    /// _Not supported in UASM._
    PrimitiveArray(UdonType, OdinPrimitiveArray),
    /// Calculated integer.
    /// _Limited support in UASM -- all integers written as UInt._
    I(OdinIntType, UdonInt),
    /// .NET type.
    /// String is used because Udon Assembly can't represent this anyway.
    /// _Not supported in UASM._
    RType(String),
    /// UdonGameObjectComponentHeapReference is used for Const This.
    UdonGameObjectComponentHeapReference(UdonType),
    /// Inserted Odin AST.
    /// _Not supported in UASM._
    OdinASTInsert(UdonOdinASTInsert),
    /// Inserted Odin AST struct.
    /// _Not supported in UASM._
    OdinASTStruct(OdinASTStruct),
}

impl From<OdinPrimitive> for UdonHeapValue {
    fn from(value: OdinPrimitive) -> Self {
        Self::P(value)
    }
}
impl From<UdonType> for UdonHeapValue {
    fn from(value: UdonType) -> Self {
        Self::RType(value.odin_name.to_string())
    }
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
    pub parameters: Vec<UdonNetworkCallParameter>,
}

/// Network call parameter; a name and type. UdonType is used for convenience.
pub type UdonNetworkCallParameter = (String, UdonType);

/// This does not match neatly with IUdonProgram; it really translates best to a `.udonjson` file.
#[derive(Clone, Debug, Serialize, Deserialize, Default)]
pub struct UdonProgram {
    /// Instructions; the actual bytecode.
    pub code: Vec<UdonInt>,
    /// Minimum heap capacity. This is used to emulate.
    /// Defaults to 1.
    /// _Ignored in UASM._
    pub min_heap_capacity: Option<u32>,
    /// Data
    pub data: Vec<UdonHeapSlot>,
    /// This is a reasonably literal transliteration into this assembler's world.
    pub code_syms: Vec<UdonSymbol>,
    /// This is similar.
    pub data_syms: Vec<UdonSymbol>,
    /// This mapping is hacky but should work. Values correspond to [kudoninfo::UDON_INTERPOLATIONS].
    pub sync_metadata: Vec<(String, String, u64)>,
    /// Behold, network call metadata.
    /// _Not supported in UASM._
    pub network_call_metadata: Option<Vec<UdonNetworkCallMetadata>>,
    /// 'Internal' symbol table.
    /// This symbol table is not written out, only used in UdonInt calculations.
    /// When writing to Udon Assembly, the strings here are _not used._
    pub internal_syms: BTreeMap<String, i64>,
    /// Update order.
    pub update_order: UdonInt,
}

mod emit_odin;
pub use emit_odin::*;

mod uasm_writer;
pub use uasm_writer::*;

mod emit_uasm;
pub use emit_uasm::*;

#[cfg(test)]
mod tests;
