use crate::{UdonDBEntry, UdonDBRef, udondbentry_impl, udondbref_getters};
use serde::{Deserialize, Serialize};
use std::borrow::Cow;
use std::collections::BTreeMap;
use std::sync::OnceLock;

#[derive(Clone, Copy, Debug, PartialEq, Eq, PartialOrd, Ord, Serialize, Deserialize)]
pub enum UdonTypeKind {
    Object,
    Struct,
    Array,
    Primitive,
    Interface,
    Enum,
}

/// Type metadata.
#[derive(Clone, PartialEq, Eq, PartialOrd, Ord, Serialize, Deserialize)]
pub struct UdonType {
    /// Udon type.
    pub name: Cow<'static, String>,
    /// Kind.
    pub kind: UdonTypeKind,
    /// Odin/shortened .NET type name.
    pub odin_name: Cow<'static, String>,
    /// 'Sync type'. This is used for, among other things, network call RPC.
    pub sync_type: Option<i32>,
    /// Enum values.
    pub enum_values: Option<BTreeMap<String, i32>>,
}

udondbentry_impl!(UdonType, udontype_map);

/// Reference to an UdonType.
pub type UdonTypeRef = UdonDBRef<UdonType>;

impl UdonType {
    /// Infers the assembly (or 'unknown')
    pub fn assembly(&self) -> &str {
        if let Some(v) = &self.odin_name.rfind(',') {
            &self.odin_name[(v + 1)..].trim_ascii()
        } else {
            "unknown"
        }
    }
    /// Gets the 'unqualified name'.
    pub fn unqualified(&self) -> &str {
        let generic_mark = self.odin_name.find('`').unwrap_or(self.odin_name.len());
        let comma_mark = self.odin_name.find(',').unwrap_or(self.odin_name.len());
        &self.odin_name[..generic_mark.min(comma_mark)]
    }
    /// Gets the 'short name' (last dotted part etc.)
    pub fn short_name(&self) -> &str {
        let ug = self.unqualified();
        &ug[(ug.rfind('.').map(|v| v + 1).unwrap_or(0))..]
    }
}

// To 'simplify' things, these implementations are somewhat cursed.

impl std::fmt::Debug for UdonType {
    fn fmt(&self, f: &mut std::fmt::Formatter<'_>) -> std::fmt::Result {
        f.write_str("UdonType::")?;
        f.write_str(&self.name)
    }
}

static UDONTYPE_MAP: OnceLock<BTreeMap<String, UdonType>> = OnceLock::new();
static UDONTYPE_MAXLEN: OnceLock<usize> = OnceLock::new();

/// Gets an [UdonType] [BTreeMap].
/// This maps the Udon type name to the corresponding [UdonType].
pub fn udontype_map() -> &'static BTreeMap<String, UdonType> {
    UDONTYPE_MAP.get_or_init(|| {
        let mut hm: BTreeMap<String, UdonType> = BTreeMap::new();
        for key in &kudon_apijson::type_names() {
            let typeobj = kudon_apijson::type_by_name(key).unwrap();
            let type_kind = typeobj["kind"].as_str().unwrap();
            let value = UdonType {
                name: Cow::Owned(key.to_string()),
                kind: if type_kind.eq("OBJECT") {
                    UdonTypeKind::Object
                } else if type_kind.eq("STRUCT") {
                    UdonTypeKind::Struct
                } else if type_kind.eq("ARRAY") {
                    UdonTypeKind::Array
                } else if type_kind.eq("PRIMITIVE") {
                    UdonTypeKind::Primitive
                } else if type_kind.eq("INTERFACE") {
                    UdonTypeKind::Interface
                } else if type_kind.eq("ENUM") {
                    UdonTypeKind::Enum
                } else {
                    panic!(
                        "type kinds should never be unparsable: {:?} {:?}",
                        key, type_kind
                    )
                },
                odin_name: Cow::Owned(typeobj["odin_name"].as_str().unwrap().to_string()),
                sync_type: typeobj["sync_type"].as_i32(),
                enum_values: if typeobj["enum_values"].is_object() {
                    let mut res = BTreeMap::new();
                    for v in typeobj["enum_values"].entries() {
                        res.insert(v.0.to_string(), v.1.as_i32().unwrap());
                    }
                    Some(res)
                } else {
                    None
                },
            };
            hm.insert(key.to_string(), value);
        }
        hm
    })
}

/// Maximum type name length.
pub fn udontype_maxlen() -> usize {
    *UDONTYPE_MAXLEN.get_or_init(|| {
        let mut len: usize = 0;
        for v in udontype_map() {
            len = len.max(v.0.len());
        }
        len
    })
}

udondbref_getters!(UdonType, udontype_get, udontyperef_get, udontype_map);

/// May be replaced with a proc macro in future that verifies at compile-time.
#[macro_export]
macro_rules! udontyperef {
    ($id:ident) => {
        (kudoninfo::udontyperef_get(stringify!($id)).unwrap())
    };
}
