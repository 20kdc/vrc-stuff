//! `kudonast`'s job is to provide an 'Udon AST'.
//! This AST is not designed for complete round-trip 1:1 conversion (for that, maybe consider `kudonodin`).
//! This AST is designed to be easily assembled to, but decently flexible.

/// The outermost Udon program structure.
/// This is the type we consider 'authoritative' for an Udon Program; in particular it's what `AbstractUdonProgramSource` outputs.
#[derive(Clone, Default)]
pub struct SerializedUdonProgramAsset {}

/// Reference to a Unity object.
#[derive(Clone)]
pub enum UdonUnityObject {
    Ref(String, i64),
}
