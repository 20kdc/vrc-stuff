//! `kudoninfo` represents a Rust reification of some of the output of `datamine2json.py`, plus some additional core structures.
//! Presently, the focus is on providing just enough information for a complete assembler.

mod refs;
pub use refs::*;

mod opcode;
pub use opcode::*;

mod trivial;
pub use trivial::*;

mod types;
pub use types::*;

mod externs;
pub use externs::*;

include!(concat!(env!("OUT_DIR"), "/generated.rs"));

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
mod tests;
