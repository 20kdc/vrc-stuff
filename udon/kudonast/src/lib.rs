//! `kudonast`'s job is to provide an 'Udon AST'.
//! This AST is not designed for complete round-trip 1:1 conversion (for that, maybe consider `kudonodin`).
//! This AST is designed to be easily assembled to, but decently flexible.

use kudoninfo::{UdonOpcode, UdonSpaciality, UdonType, UdonTypeRef, udontyperef};
use kudonodin::*;
use serde::{Deserialize, Serialize};
use std::collections::{BTreeMap, HashSet};

// -- part 1: linker value resolver --

/// Calculatable integer in the Udon AST.
/// In a linked program, all UdonInt instances are replaced with just the one kind.
#[derive(Clone, Debug, PartialEq, Eq, PartialOrd, Ord, Serialize, Deserialize)]
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

impl From<i64> for UdonInt {
    fn from(value: i64) -> Self {
        Self::I(value)
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
    pub udon_type: Option<UdonTypeRef>,
    pub address: UdonInt,
    pub mode: UdonAccess,
}

/// Reference to a Unity object.
#[derive(Clone, Debug, PartialEq, PartialOrd, Serialize, Deserialize)]
pub enum UdonUnityObject {
    Ref(String, i64),
}

/// Used to insert arbitrary data.
#[derive(Clone, Debug, PartialEq, PartialOrd, Serialize, Deserialize)]
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
#[derive(Clone, Debug, PartialEq, PartialOrd, Serialize, Deserialize)]
pub enum UdonHeapValue {
    /// Odin primitive.
    /// _Limited support in UASM -- all integers written as UInt._
    P(OdinPrimitive),
    /// Primitive array with the encompassing object auto-generated.
    /// This is the most convenient way to handle, i.e. [kudoninfo::udon_types::SystemByteArray].
    /// _Not supported in UASM._
    PrimitiveArray(UdonTypeRef, OdinPrimitiveArray),
    /// Calculated integer.
    /// _Limited support in UASM -- all integers written as UInt._
    I(OdinIntType, UdonInt),
    /// .NET type.
    /// String is used because Udon Assembly can't represent this anyway.
    /// _Not supported in UASM._
    RType(String),
    /// UdonGameObjectComponentHeapReference is used for Const This.
    /// This causes special behaviour in slot handling.
    This,
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

pub fn odininttype_to_udontype(oit: OdinIntType) -> UdonTypeRef {
    match oit {
        OdinIntType::SByte => udontyperef!(SystemSByte),
        OdinIntType::Byte => udontyperef!(SystemByte),
        OdinIntType::Short => udontyperef!(SystemInt16),
        OdinIntType::UShort => udontyperef!(SystemUInt16),
        OdinIntType::Int => udontyperef!(SystemInt32),
        OdinIntType::UInt => udontyperef!(SystemUInt32),
        OdinIntType::Long => udontyperef!(SystemInt64),
        OdinIntType::ULong => udontyperef!(SystemUInt64),
        OdinIntType::Bool => udontyperef!(SystemBoolean),
        OdinIntType::Char => udontyperef!(SystemChar),
    }
}

/// Udon heap slot.
/// Note that UdonGameObjectComponentHeapReference 'supplants' the type in an unusual way.
#[derive(Clone, Debug, Serialize, Deserialize)]
pub struct UdonHeapSlot(pub UdonTypeRef, pub UdonHeapValue);

/// Network call metadata.
#[derive(Clone, Debug, Serialize, Deserialize)]
pub struct UdonNetworkCallMetadata {
    pub name: String,
    pub max_events_per_second: i32,
    pub parameters: Vec<UdonNetworkCallParameter>,
}

/// Network call parameter; a name and type. [UdonTypeRef] is used for convenience instead of the sync type.
pub type UdonNetworkCallParameter = (String, UdonTypeRef);

/// This does not match neatly with IUdonProgram; it really translates best to a `.udonjson` file.
#[derive(Clone, Debug, Serialize, Deserialize, Default)]
pub struct UdonProgram {
    /// Instructions; the actual bytecode.
    #[serde(default)]
    pub code: Vec<UdonInt>,
    /// Comments on code.
    /// These are indexed by code array index, not multiplied by 4.
    /// _UASM only._
    #[serde(default)]
    pub code_comments: BTreeMap<usize, String>,
    /// Minimum heap capacity. This is used to emulate.
    /// Defaults to 1.
    /// _Ignored in UASM._
    pub min_heap_capacity: Option<u32>,
    /// Data
    #[serde(default)]
    pub data: Vec<UdonHeapSlot>,
    /// Comments on data.
    /// _UASM only._
    #[serde(default)]
    pub data_comments: BTreeMap<usize, String>,
    /// This is a reasonably literal transliteration into this assembler's world.
    #[serde(default)]
    pub code_syms: Vec<UdonSymbol>,
    /// This is similar.
    #[serde(default)]
    pub data_syms: Vec<UdonSymbol>,
    /// This mapping is hacky but should work. Values correspond to [kudoninfo::UDON_INTERPOLATIONS].
    #[serde(default)]
    pub sync_metadata: Vec<(String, String, u64)>,
    /// Behold, network call metadata.
    /// _Not supported in UASM._
    #[serde(default)]
    pub network_call_metadata: Vec<UdonNetworkCallMetadata>,
    /// 'Internal' symbol table.
    /// This symbol table is not written out, only used in UdonInt calculations.
    /// The symbols aren't even seen in UASM.
    /// The strings here are only used as keys.
    /// There are a few 'canonical' symbol namespaces used by provided utility functions:
    /// `_extern.Example.Example` : The extern of the given name.
    /// `_string.Hello, world!` : The string of the given text.
    /// `_example_gensym0`: Unique symbols. See gensym function.
    #[serde(default)]
    pub internal_syms: BTreeMap<String, i64>,
    /// Update order.
    #[serde(default)]
    pub update_order: UdonInt,
    /// Gensym number.
    #[serde(default)]
    pub gensym_number: u64,
}

/// So this used to be in KU2, but it was more syntactically convenient to embed the info into the declaration type.
/// Meanwhile more convenience logic got merged into UdonProgram and this happened.
#[derive(Clone, Copy, Debug, PartialEq, Eq, PartialOrd, Ord, Serialize, Deserialize)]
pub enum UdonAccess {
    /// Removable in Odin
    Elidable,
    Symbol,
    Public,
}

impl UdonProgram {
    fn verify_symbol_table(&self, v: &Vec<UdonSymbol>) -> Result<(), String> {
        let mut map = HashSet::new();
        for value in v {
            let resolved = value.address.resolve(&self.internal_syms)?;
            if !map.insert(resolved) {
                return Err(format!("Symtab duplicate at: {}", value.name));
            }
        }
        Ok(())
    }
    pub fn verify(&self) -> Result<(), String> {
        Self::verify_symbol_table(self, &self.code_syms)?;
        Self::verify_symbol_table(self, &self.data_syms)?;
        Ok(())
    }
    /// Ensures the presence of the given string or extern, returning the resulting internal symbol name.
    pub fn ensure_string(&mut self, ext: &str, is_ext: bool) -> String {
        let key = if is_ext {
            format!("_extern__{}", ext.replace(".", "_dot_"))
        } else {
            format!("_string.{}", ext)
        };
        if !self.internal_syms.contains_key(&key) {
            let residx = self.data.len();
            self.data.push(UdonHeapSlot(
                udontyperef!(SystemString).into(),
                UdonHeapValue::P(OdinPrimitive::String(ext.to_string())),
            ));
            if is_ext {
                self.data_syms.push(UdonSymbol {
                    name: key.clone(),
                    udon_type: Some(udontyperef!(SystemString)),
                    address: UdonInt::Sym(key.clone()),
                    mode: UdonAccess::Elidable,
                });
            }
            self.internal_syms.insert(key.clone(), residx as i64);
        }
        key
    }
    /// Ensures the presence of the given integer constant, returning the resulting internal symbol name.
    pub fn ensure_iconst(&mut self, it: OdinIntType, v: i64) -> String {
        let key = format!("_int__{}_{}", it.mangle_name(), v).replace("-", "m");
        if !self.internal_syms.contains_key(&key) {
            let residx = self.data.len();
            self.data.push(UdonHeapSlot(
                odininttype_to_udontype(it),
                UdonHeapValue::I(it, UdonInt::I(v)),
            ));
            self.data_syms.push(UdonSymbol {
                name: key.clone(),
                udon_type: Some(udontyperef!(SystemString)),
                address: UdonInt::Sym(key.clone()),
                mode: UdonAccess::Elidable,
            });
            self.internal_syms.insert(key.clone(), residx as i64);
        }
        key
    }
    /// Generates a uniqueish symbol.
    pub fn gensym(&mut self, r: &str) -> String {
        let num = self.gensym_number;
        self.gensym_number += 1;
        format!("_{}_gensym{}", r, num)
    }
    /// Comment.
    pub fn add_comment(code_comments: &mut BTreeMap<usize, String>, at: usize, comm: &str) {
        if let Some(ci) = code_comments.get_mut(&at) {
            ci.push_str("\n");
            ci.push_str(comm);
        } else {
            code_comments.insert(at, comm.to_string());
        }
    }

    /// Mirror of ye olde UASM declare_heap method.
    pub fn declare_heap(
        &mut self,
        name: &impl ToString,
        public: Option<UdonAccess>,
        ty: impl Into<UdonTypeRef>,
        val: impl Into<UdonHeapValue>,
    ) -> Result<(), String> {
        let place = self.data.len() as i64;
        let name = name.to_string();
        let ty: UdonTypeRef = ty.into();
        self.data.push(UdonHeapSlot(ty.clone(), val.into()));
        if self.internal_syms.contains_key(&name) {
            return Err(format!(
                "Heap label would overwrite internal sym '{}'",
                name
            ));
        }
        self.internal_syms.insert(name.to_string(), place);
        if let Some(mode) = public {
            self.data_syms.push(UdonSymbol {
                name: name.to_string(),
                udon_type: Some(ty),
                address: UdonInt::Sym(name.to_string()),
                mode,
            });
        }
        Ok(())
    }

    pub fn declare_heap_i(
        &mut self,
        name: &impl ToString,
        public: Option<UdonAccess>,
        ty: OdinIntType,
        val: impl Into<UdonInt>,
    ) -> Result<(), String> {
        self.declare_heap(
            name,
            public,
            odininttype_to_udontype(ty),
            UdonHeapValue::I(ty, val.into()),
        )
    }

    /// Another UASMWriter rescue.
    pub fn code_label(
        &mut self,
        name: &impl ToString,
        public: Option<UdonAccess>,
    ) -> Result<(), String> {
        let place = (self.code.len() * 4) as i64;
        let name = name.to_string();
        if self.internal_syms.contains_key(&name) {
            return Err(format!(
                "Code label would overwrite internal sym '{}'",
                name
            ));
        }
        self.internal_syms.insert(name.to_string(), place);
        if let Some(mode) = public.into() {
            self.code_syms.push(UdonSymbol {
                name: name.to_string(),
                udon_type: None,
                address: UdonInt::Sym(name.to_string()),
                mode,
            });
        }
        Ok(())
    }
}

/// Simplified assembly to match the old [UASMWriter] API.
#[macro_export]
macro_rules! uasm_op {
    ($asm:expr, $op:ident) => {
        $asm.code
            .push(kudonast::UdonInt::Op(&kudoninfo::opcodes::$op));
    };
    ($asm:expr, $op:ident, $arg:expr) => {
        $asm.code
            .push(kudonast::UdonInt::Op(&kudoninfo::opcodes::$op));
        {
            let arg = $arg.to_string();
            $asm.code.push(kudonast::UdonInt::Sym(arg));
        }
    };
}

#[macro_export]
macro_rules! uasm_op_i {
    ($asm:expr, $op:ident, $arg:expr) => {
        $asm.code
            .push(kudonast::UdonInt::Op(&kudoninfo::opcodes::$op));
        {
            let arg: i64 = $arg;
            $asm.code.push(kudonast::UdonInt::I(arg));
        }
    };
}

#[macro_export]
macro_rules! uasm_stop {
    ($asm:expr) => {
        uasm_op_i!($asm, JUMP, 0xFFFFFFFC);
    };
}

// 'raw' Odin parsed structs

mod odin_program;
pub use odin_program::*;

mod odin_symtab;
pub use odin_symtab::*;

mod odin_synctab;
pub use odin_synctab::*;

// main emitter/reader modules

mod emit_odin;
pub use emit_odin::*;

mod uasm_writer;
pub use uasm_writer::*;

mod emit_uasm;
pub use emit_uasm::*;

mod read_odin;
pub use read_odin::*;

// tests

#[cfg(test)]
mod tests;
