pub trait UdonType {}

pub trait UdonCastable<V>: UdonType {}

pub mod types {
    use super::{UdonCastable, UdonType};
    use kip32_macros::kip32_internal_udontypes;
    kip32_internal_udontypes!();
}
