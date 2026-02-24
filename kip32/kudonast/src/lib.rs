//! `kudonast`'s job is to provide an 'Udon AST'.
//! This AST is not designed for complete round-trip 1:1 conversion (for that, maybe consider `kudonodin`).
//! This AST is designed to be easily assembled to, but decently flexible.

use kudoninfo::UdonType;
use std::collections::BTreeMap;

/// Integer in the Udon AST.
/// Notably, these integers can be calculated.
#[derive(Clone, Debug)]
pub enum UdonInt {}

/// Udon instruction enum.
#[derive(Clone, Debug)]
pub enum UdonInstruction {
    Nop,
    Push(UdonInt),
    Pop,
    JumpIfFalse(UdonInt),
    Jump(UdonInt),
    Extern(UdonInt),
    Annotation(UdonInt),
    JumpIndirect(UdonInt),
    Copy,
    /// Pseudoinstruction: Places an internal symbol at the current program position.
    PlaceInternalSym(usize),
}

/// Reference to a Unity object.
#[derive(Clone)]
pub enum UdonUnityObject {
    Ref(String, i64),
}

/// Udon symbol.
pub struct UdonSymbol {
    pub udon_type: Option<UdonType>,
    /// To make debugging easier, all Udon symbols are required to have corresponding internal symbols.
    pub address_internal_sym: usize,
    pub public: bool,
}

/// Udon heap slot.
pub struct UdonHeapSlot {}

/// This does not match neatly with IUdonProgram; it really translates best to a `.udonjson` file.
#[derive(Clone, Debug, Default)]
pub struct UdonProgram {
    /// Instructions; the actual bytecode.
    pub code: Vec<UdonInstruction>,
    /// Data
    pub data: Vec<UdonHeapSlot>,
    /// This is a reasonably literal transliteration into this assembler's world.
    pub code_syms: BTreeMap<String, UdonSymbol>,
    /// This is similar.
    pub data_syms: BTreeMap<String, UdonSymbol>,
    /// 'Internal' symbol table.
    /// The strings exist for debugging purposes only (unplaced symbol errors).
    /// This symbol table is not written out, only used in UdonInt calculations.
    pub internal_syms: Vec<(String, Option<i32>)>,
}
