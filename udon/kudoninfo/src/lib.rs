//! `kudoninfo` represents a Rust reification of some of the output of `datamine2json.py`, plus some additional core structures.
//! Presently, the focus is on providing just enough information for a complete assembler.

use serde::{Deserialize, Serialize};
use std::borrow::Cow;
use std::collections::BTreeMap;
use std::rc::Rc;
use std::sync::OnceLock;

#[derive(Clone, Copy, Debug, PartialEq, Eq, PartialOrd, Ord)]
pub enum UdonTypeKind {
    Object,
    Struct,
    Array,
    Primitive,
    Interface,
    Enum
}

/// Type metadata.
#[derive(Clone, PartialEq, Eq, PartialOrd, Ord)]
pub struct UdonType {
    /// Udon type.
    pub name: Cow<'static, String>,
    /// Kind.
    pub kind: UdonTypeKind,
    /// Odin/shortened .NET type name.
    pub odin_name: Cow<'static, String>,
    /// 'Sync type'. This is used for, among other things, network call RPC.
    pub sync_type: Option<i32>,
}

impl UdonType {
    /// Infers the assembly (or 'unknown')
    pub fn assembly(&self) -> &str {
        if let Some(v) = &self.odin_name.rfind(',') {
            &self.odin_name[(v + 1)..].trim_ascii()
        } else {
            "unknown"
        }
    }
    /// Gets the 'unqualified name'.
    pub fn unqualified(&self) -> &str {
        let generic_mark = self.odin_name.find('`').unwrap_or(self.odin_name.len());
        let comma_mark = self.odin_name.find(',').unwrap_or(self.odin_name.len());
        &self.odin_name[..generic_mark.min(comma_mark)]
    }
    /// Gets the 'short name' (last dotted part etc.)
    pub fn short_name(&self) -> &str {
        let ug = self.unqualified();
        &ug[(ug.rfind('.').map(|v| v + 1).unwrap_or(0))..]
    }
}

/// Reference to an Udon type.
#[derive(Clone, Debug, PartialEq, Eq, PartialOrd, Ord)]
pub enum UdonTypeRef {
    C(&'static UdonType),
    R(Rc<UdonType>),
}

impl Serialize for UdonTypeRef {
    fn serialize<S>(&self, serializer: S) -> Result<S::Ok, S::Error>
    where
        S: serde::Serializer,
    {
        let r: &UdonType = self.as_ref();
        r.serialize(serializer)
    }
}

impl From<&'static UdonType> for UdonTypeRef {
    fn from(value: &'static UdonType) -> Self {
        Self::C(value)
    }
}

impl From<UdonType> for UdonTypeRef {
    fn from(value: UdonType) -> Self {
        Self::R(Rc::new(value))
    }
}

impl std::ops::Deref for UdonTypeRef {
    type Target = UdonType;
    fn deref(&self) -> &Self::Target {
        match self {
            Self::C(v) => v,
            Self::R(v) => v,
        }
    }
}

impl AsRef<UdonType> for UdonTypeRef {
    fn as_ref(&self) -> &UdonType {
        match self {
            Self::C(v) => v,
            Self::R(v) => v,
        }
    }
}

// To 'simplify' things, these implementations are somewhat cursed.

impl std::fmt::Debug for UdonType {
    fn fmt(&self, f: &mut std::fmt::Formatter<'_>) -> std::fmt::Result {
        f.write_str("UdonType::")?;
        f.write_str(&self.name)
    }
}

impl serde::Serialize for UdonType {
    fn serialize<S>(&self, serializer: S) -> Result<S::Ok, S::Error>
    where
        S: serde::Serializer,
    {
        serializer.serialize_str(&self.name)
    }
}

impl<'de> serde::Deserialize<'de> for UdonType {
    fn deserialize<D>(deserializer: D) -> Result<Self, D::Error>
    where
        D: serde::Deserializer<'de>,
    {
        // this is poor construction, but it should do
        let string = String::deserialize(deserializer)?;
        udontype_get(&string)
            .ok_or(serde::de::Error::custom("invalid UdonType"))
            .map(|v| v.clone())
    }
}

impl<'de> Deserialize<'de> for UdonTypeRef {
    fn deserialize<D>(deserializer: D) -> Result<Self, D::Error>
    where
        D: serde::Deserializer<'de>,
    {
        // this is poor construction, but it should do
        let string = String::deserialize(deserializer)?;
        udontype_get(&string)
            .ok_or(serde::de::Error::custom("invalid UdonType"))
            .map(|v| UdonTypeRef::C(v))
    }
}

/// Spaciality is meant to aid in assembly emit.
/// It's also used by kudonasm to control default string affinity.
#[derive(Clone, Copy, PartialEq, Eq, PartialOrd, Ord, Debug, Serialize, Deserialize)]
pub enum UdonSpaciality {
    Indistinct,
    Code,
    Data,
    DataExtern,
    Annotation,
}

/// Opcode metadata.
/// These are static; Udon is never getting new opcodes.
/// Accordingly, serialize/deserialize are applied specifically to `&'static UdonOpcode`.
/// It's best understood as a Java-style enum.
#[derive(Clone, Copy, PartialEq, Eq, PartialOrd, Ord, Debug)]
pub struct UdonOpcode {
    pub name: &'static str,
    pub opcode: u32,
    pub parameters: &'static [UdonSpaciality],
}

impl Serialize for &'static UdonOpcode {
    fn serialize<S>(&self, serializer: S) -> Result<S::Ok, S::Error>
    where
        S: serde::Serializer,
    {
        serializer.serialize_str(self.name)
    }
}

impl<'de> Deserialize<'de> for &'static UdonOpcode {
    fn deserialize<D>(deserializer: D) -> Result<Self, D::Error>
    where
        D: serde::Deserializer<'de>,
    {
        let string = String::deserialize(deserializer)?;
        udonopcode_get(&string).ok_or(serde::de::Error::custom("invalid UdonOpcode"))
    }
}

include!(concat!(env!("OUT_DIR"), "/generated.rs"));

static UDONTYPE_MAP: OnceLock<BTreeMap<String, UdonType>> = OnceLock::new();

/// Gets an [UdonType] [BTreeMap].
/// This maps the Udon type name to the corresponding [UdonType].
pub fn udontype_map() -> &'static BTreeMap<String, UdonType> {
    UDONTYPE_MAP.get_or_init(|| {
        let mut hm: BTreeMap<String, UdonType> = BTreeMap::new();
        for key in &kudon_apijson::type_names() {
            let typeobj = kudon_apijson::type_by_name(key).unwrap();
            let type_kind = typeobj["kind"].as_str().unwrap();
            let value = UdonType {
                name: Cow::Owned(key.to_string()),
                kind: if type_kind.eq("OBJECT") {
                    UdonTypeKind::Object
                } else if type_kind.eq("STRUCT") {
                    UdonTypeKind::Struct
                } else if type_kind.eq("ARRAY") {
                    UdonTypeKind::Array
                } else if type_kind.eq("PRIMITIVE") {
                    UdonTypeKind::Primitive
                } else if type_kind.eq("INTERFACE") {
                    UdonTypeKind::Interface
                } else if type_kind.eq("ENUM") {
                    UdonTypeKind::Enum
                } else {
                    panic!("type kinds should never be unparsable: {:?} {:?}", key, type_kind)
                },
                odin_name: Cow::Owned(typeobj["odin_name"].as_str().unwrap().to_string()),
                sync_type: typeobj["sync_type"].as_i32(),
            };
            hm.insert(key.to_string(), value);
        }
        hm
    })
}

/// Information about an extern.
pub struct UdonExtern {
    pub associated_type: Cow<'static, String>,
    pub name: Cow<'static, String>
}

static UDONEXTERN_MAP: OnceLock<BTreeMap<String, UdonExtern>> = OnceLock::new();

/// Gets an [UdonType] [BTreeMap].
/// This maps the Udon type name to the corresponding [UdonType].
pub fn udonextern_map() -> &'static BTreeMap<String, UdonExtern> {
    UDONEXTERN_MAP.get_or_init(|| {
        let mut hm: BTreeMap<String, UdonExtern> = BTreeMap::new();
        for key in udontype_map() {
            let typeobj = kudon_apijson::type_by_name(key.0).unwrap();
            for ext in typeobj["externs"].entries() {
                hm.insert(ext.0.to_string(), UdonExtern {
                    associated_type: Cow::Owned(key.0.to_string()),
                    name: Cow::Owned(ext.0.to_string())
                });
            }
        }
        hm
    })
}

/// Gets an [UdonType].
/// This is used in deserialization.
pub fn udontype_get(b: &str) -> Option<&'static UdonType> {
    if let Some(res) = udontype_map().get(b) {
        Some(res)
    } else {
        None
    }
}

/// Gets an [UdonTypeRef].
pub fn udontyperef_get(b: &str) -> Option<UdonTypeRef> {
    if let Some(res) = udontype_map().get(b) {
        Some(UdonTypeRef::C(res))
    } else {
        None
    }
}

/// May be replaced with a proc macro in future that verifies at compile-time.
#[macro_export]
macro_rules! udontype {
    ($id:ident) => {
        (kudoninfo::udontype_get(stringify!($id)).unwrap())
    };
}
/// May be replaced with a proc macro in future that verifies at compile-time.
#[macro_export]
macro_rules! udontyperef {
    ($id:ident) => {
        (kudoninfo::udontyperef_get(stringify!($id)).unwrap())
    };
}

/// Gets an [UdonOpcode]. This does a linear search.
pub fn udonopcode_get(b: &str) -> Option<&'static UdonOpcode> {
    for v in UDON_OPCODES {
        if let Some(v) = v {
            if v.name.eq(b) {
                return Some(v);
            }
        }
    }
    None
}

/// Every opcode in Udon, in a module for compile-time access.
pub mod opcodes {
    use crate::{UdonOpcode, UdonSpaciality};
    macro_rules! def_opcode {
        ($opcode:literal, $name:ident, $params:expr) => {
            pub static $name: UdonOpcode = UdonOpcode {
                name: stringify!($name),
                opcode: $opcode,
                parameters: $params,
            };
        };
    }
    def_opcode!(0, NOP, &[]);
    def_opcode!(1, PUSH, &[UdonSpaciality::Data]);
    def_opcode!(2, POP, &[]);
    // 3 omitted
    def_opcode!(4, JUMP_IF_FALSE, &[UdonSpaciality::Code]);
    def_opcode!(5, JUMP, &[UdonSpaciality::Code]);
    def_opcode!(6, EXTERN, &[UdonSpaciality::DataExtern]);
    def_opcode!(7, ANNOTATION, &[UdonSpaciality::Annotation]);
    def_opcode!(8, JUMP_INDIRECT, &[UdonSpaciality::Data]);
    def_opcode!(9, COPY, &[]);
}

/// Every opcode in Udon, as a sparse table (see [sparse_table_get]).
pub static UDON_OPCODES: &[Option<&'static UdonOpcode>] = &[
    Some(&opcodes::NOP),
    Some(&opcodes::PUSH),
    Some(&opcodes::POP),
    None,
    Some(&opcodes::JUMP_IF_FALSE),
    Some(&opcodes::JUMP),
    Some(&opcodes::EXTERN),
    Some(&opcodes::ANNOTATION),
    Some(&opcodes::JUMP_INDIRECT),
    Some(&opcodes::COPY),
];

pub mod interpolations {
    pub const NONE: u64 = 0;
    pub const LINEAR: u64 = 1;
    pub const SMOOTH: u64 = 2;
}

/// Every interpolation in Udon, in uasm form, as a sparse table (see [sparse_table_get]).
pub static UDON_INTERPOLATIONS: &[Option<&'static str>] =
    &[Some("none"), Some("linear"), Some("smooth")];

/// Looks up a value from a sparse table.
pub fn sparse_table_get<T: Copy>(table: &[Option<T>], index: usize) -> Option<T> {
    if let Some(v) = table.get(index) {
        // This is a pretty weird operation; we're implicitly Copy-ing the `&'static T` here.
        *v
    } else {
        None
    }
}

#[cfg(test)]
mod tests {
    #[test]
    pub fn test_udontype_lookups() {
        _ = crate::udontype_get("SystemInt32").unwrap();
        _ = crate::udontype_get("SystemUInt32").unwrap();
        _ = crate::udontype_get("VRCSDKBaseVRCRenderTexture").unwrap();
    }
}
