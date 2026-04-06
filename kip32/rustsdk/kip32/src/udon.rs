use crate::kip32_syscall;

pub trait UdonType: Copy {}

pub trait UdonCastable<V>: UdonType {}

pub mod types {
    use super::{UdonCastable, UdonType};
    use kip32_macros::kip32_internal_udontypes;
    kip32_internal_udontypes!();
}

/// Udon value reference.
/// Notably, this is usable for constants.
pub trait UdonValue {
    type Type: UdonType;
    /// Pushes to the Udon stack.
    fn push();
}

/// Pops a value from the Udon stack.
pub fn pop() {
    kip32_syscall!(b"stdsyscall_pop");
}

/// Mutable Udon value reference.
/// Not usable for constants.
pub trait UdonMutValue : UdonValue {
    /// Copies from the given Udon heap slot to this one.
    #[inline(always)]
    fn copy<Src: UdonValue>() where Src::Type : UdonCastable<Self::Type> {
        Src::push();
        Self::push();
        kip32_syscall!(b"stdsyscall_copy");
    }
}

#[macro_export]
macro_rules! kip32_udon_val {
    ($id: ident, $ty: ty, $ex: expr) => {
        pub enum $id {}
        impl $crate::udon::UdonValue for $id {
            type Type = $ty;
            #[inline(always)]
            fn push() {
                $crate::kip32_syscall!($crate::kip32_bytesconcat!(b"builtin_push_", $ex));
            }
        }
    };
}
