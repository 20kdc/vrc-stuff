//! `kudonast`'s job is to provide an 'Udon AST'.
//! This AST is not designed for complete round-trip 1:1 conversion (for that, maybe consider `kudonodin`).
//! This AST is designed to be easily assembled to, and highly flexible.

/// The outermost Udon program structure.
/// This is the type we consider 'authoritative' for an Udon Program; in particular it's what `AbstractUdonProgramSource` outputs.
#[derive(Default)]
pub struct SerializedUdonProgramAsset {
    //pub program: UdonProgram
}
