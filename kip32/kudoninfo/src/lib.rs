//! `kudoninfo` represents a Rust reification of some of the output of `datamine2json.py`.
//! Presently, the focus is on providing just enough information for a complete assembler.

use std::collections::HashMap;

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
