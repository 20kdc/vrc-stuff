use serde::{Deserialize, Serialize};

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
