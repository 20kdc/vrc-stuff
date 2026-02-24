//! `kudoninfo` represents a Rust reification of some of the output of `datamine2json.py`.
//! Presently, the focus is on providing just enough information for a complete assembler.

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
