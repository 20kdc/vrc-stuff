//! This crate represents types which can be either independently constructed (for workarounds) or, well, not.
use serde::{Deserialize, Serialize};
use std::sync::Arc;

/// Something kudoninfo has a database of.
/// Due to the nature of the VRChat platform, unexpected database entries are _not expected to come up often._
/// This implies that if you need one, you should be patching at least as far back as `kudon_apijson` level or further.
pub trait UdonDBEntry: Clone + 'static {
    /// Finds an entry by name.
    fn get_builtin(name: &str) -> Option<&'static Self>;
    /// Gets the name of this entry.
    /// This may be valid even if the entry is not builtin.
    fn get_name(&self) -> &str;
}

/// Reference to something in kudoninfo.
#[derive(Clone, Debug, PartialEq, Eq, PartialOrd, Ord)]
pub enum UdonDBRef<Src: UdonDBEntry> {
    C(&'static Src),
    R(Arc<Src>),
}

impl<Src: UdonDBEntry> Serialize for UdonDBRef<Src> {
    fn serialize<S>(&self, serializer: S) -> Result<S::Ok, S::Error>
    where
        S: serde::Serializer,
    {
        let r: &Src = self.as_ref();
        serializer.serialize_str(r.get_name())
    }
}

impl<Src: UdonDBEntry> From<&'static Src> for UdonDBRef<Src> {
    fn from(value: &'static Src) -> Self {
        Self::C(value)
    }
}

impl<Src: UdonDBEntry> From<Src> for UdonDBRef<Src> {
    fn from(value: Src) -> Self {
        Self::R(Arc::new(value))
    }
}

impl<Src: UdonDBEntry> std::ops::Deref for UdonDBRef<Src> {
    type Target = Src;
    fn deref(&self) -> &Self::Target {
        match self {
            Self::C(v) => v,
            Self::R(v) => v,
        }
    }
}

impl<Src: UdonDBEntry> AsRef<Src> for UdonDBRef<Src> {
    fn as_ref(&self) -> &Src {
        match self {
            Self::C(v) => v,
            Self::R(v) => v,
        }
    }
}

impl<'de, Src: UdonDBEntry> Deserialize<'de> for UdonDBRef<Src> {
    fn deserialize<D>(deserializer: D) -> Result<Self, D::Error>
    where
        D: serde::Deserializer<'de>,
    {
        // this is poor construction, but it should do
        let string = String::deserialize(deserializer)?;
        Src::get_builtin(&string)
            .ok_or(serde::de::Error::custom(format!(
                "not in database: {}",
                &string
            )))
            .map(|v| Self::C(v))
    }
}

/// `udondbentry_impl!(UdonType, udontype_map);`
#[macro_export]
macro_rules! udondbentry_impl {
    ($type:ty, $type_map:ident) => {
        impl UdonDBEntry for $type {
            fn get_builtin(name: &str) -> Option<&'static Self> {
                $type_map().get(name)
            }
            fn get_name(&self) -> &str {
                &self.name
            }
        }
    };
}

#[macro_export]
macro_rules! udondbref_getters {
    ($type:ty, $type_get:ident, $typeref_get:ident, $type_map:ident) => {
        pub fn $type_get(b: &str) -> Option<&'static $type> {
            $type_map().get(b)
        }

        pub fn $typeref_get(b: &str) -> Option<UdonDBRef<$type>> {
            if let Some(res) = $type_map().get(b) {
                Some(UdonDBRef::C(res))
            } else {
                None
            }
        }
    };
}
