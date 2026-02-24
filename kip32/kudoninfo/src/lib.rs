//! `kudoninfo` represents a Rust reification of some of the output of `datamine2json.py`, plus some additional core structures.
//! Presently, the focus is on providing just enough information for a complete assembler.

use std::collections::HashMap;
use serde::{Deserialize, Serialize};

/// Type metadata.
#[derive(Clone)]
pub struct UdonType {
    /// Udon type.
    pub name: std::borrow::Cow<'static, str>,
    /// Odin/shortened .NET type name.
    pub odin_name: std::borrow::Cow<'static, str>,
    /// 'Sync type'. This is used for, among other things, network call RPC.
    pub sync_type: Option<i32>,
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
        S: serde::Serializer {
        serializer.serialize_str(&self.name)
    }
}

impl<'de> serde::Deserialize<'de> for UdonType {
    fn deserialize<D>(deserializer: D) -> Result<Self, D::Error>
    where
        D: serde::Deserializer<'de> {
        // this is poor construction, but it should do
        let string = String::deserialize(deserializer)?;
        udontype_get(&string).ok_or(serde::de::Error::custom("invalid UdonType")).map(|v| v.clone())
    }
}

/// Spaciality is meant to aid in assembly emit.
#[derive(Clone, Copy, PartialEq, Eq, PartialOrd, Ord, Debug, Serialize, Deserialize)]
pub enum UdonSpaciality {
    Indistinct,
    Code,
    Data,
    Annotation,
}

impl std::ops::Add<UdonSpaciality> for UdonSpaciality {
    type Output = UdonSpaciality;
    fn add(self, rhs: UdonSpaciality) -> Self::Output {
        // Prefer non-indistinct.
        // If unclear, choose indistinct.
        if self == rhs {
            self
        } else if self == Self::Indistinct {
            rhs
        } else if rhs == Self::Indistinct {
            self
        } else {
            Self::Indistinct
        }
    }
}

/// Opcode metadata.
/// These are static; Udon is never getting new opcodes.
/// Accordingly, serialize/deserialize are applied specifically to `&'static UdonOpcode`.
/// It's best understood as a Java-style enum.
#[derive(Clone, Copy, Debug)]
pub struct UdonOpcode {
    pub name: &'static str,
    pub opcode: u32,
    pub parameters: &'static [UdonSpaciality],
}

impl Serialize for &'static UdonOpcode {
    fn serialize<S>(&self, serializer: S) -> Result<S::Ok, S::Error>
    where
        S: serde::Serializer {
        serializer.serialize_str(self.name)
    }
}

impl<'de> Deserialize<'de> for &'static UdonOpcode {
    fn deserialize<D>(deserializer: D) -> Result<Self, D::Error>
    where
        D: serde::Deserializer<'de> {
        let string = String::deserialize(deserializer)?;
        udonopcode_get(&string).ok_or(serde::de::Error::custom("invalid UdonOpcode"))
    }
}

mod generated;

pub use generated::*;

/// Creates an [UdonType] [HashMap].
/// This maps the Udon type name to the corresponding [UdonType].
pub fn udontype_hashmap() -> HashMap<String, UdonType> {
    let mut hm: HashMap<String, UdonType> = HashMap::new();
    for v in UDON_TYPES {
        hm.insert(v.name.to_string(), (*v).clone());
    }
    hm
}

/// Gets an [UdonType] statically using binary search.
/// This is used in deserialization.
pub fn udontype_get(b: &str) -> Option<&'static UdonType> {
    if let Ok(res) = UDON_TYPES.binary_search_by_key(&b, |v| {
        &v.name
    }) {
        Some(UDON_TYPES[res])
    } else {
        None
    }
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
    def_opcode!(6, EXTERN, &[UdonSpaciality::Data]);
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

/// Looks up a value from a sparse table.
pub fn sparse_table_get<T>(table: &[Option<&'static T>], index: usize) -> Option<&'static T> {
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
        assert_eq!(crate::udontype_get("SystemInt32").expect("SystemInt32 must exist").name, "SystemInt32");
        assert_eq!(crate::udontype_get("SystemUInt32").expect("SystemUInt32 must exist").name, "SystemUInt32");
        assert_eq!(crate::udontype_get("VRCSDKBaseVRCRenderTexture").expect("VRCSDKBaseVRCRenderTexture must exist").name, "VRCSDKBaseVRCRenderTexture");
    }
}
