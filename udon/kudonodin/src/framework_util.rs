use crate::*;
use serde::{Deserialize, Serialize};

// -- .NET Types --

#[derive(Clone, Copy, Debug, Serialize, Deserialize, PartialEq, Eq, PartialOrd, Ord)]
pub enum OdinSTRefListKind {
    Array,
    List,
}

/// Wrapper for lists of reference types.
/// Automatically adapts between arrays and lists.
pub struct OdinSTRefList<V> {
    pub contents: Vec<V>,
    pub ty: String,
    pub kind: OdinSTRefListKind,
}

impl<V: OdinSTDeserializable> OdinSTDeserializableRefType for OdinSTRefList<V> {
    fn deserialize(src: &OdinASTRefMap, val: &OdinASTStruct) -> Result<Self, String> {
        let typefull = if let Some(v) = &val.0 {
            v
        } else {
            return Err("List must have a type".to_string());
        };
        if val.1.len() != 1 {
            return Err("List must have one (Array) entry".to_string());
        }
        let mut determine: Option<(String, OdinSTRefListKind)> = None;
        // try list detect
        if let Some(pfxrm) = typefull.strip_prefix("System.Collections.Generic.List`1[[") {
            determine = pfxrm
                .strip_suffix("]], mscorlib")
                .map(|v| (v.to_string(), OdinSTRefListKind::List));
        }
        // try array detect
        if determine.is_none() {
            if let Some(comma) = typefull.rfind(',') {
                let typepfx = &typefull[..comma];
                let assembly = &typefull[comma..];
                if let Some(typename) = typepfx.strip_suffix("[]") {
                    determine = Some((
                        format!("{}{}", typename, assembly),
                        OdinSTRefListKind::Array,
                    ));
                }
            }
        }
        // done with detect
        if let Some(determine) = determine {
            if let OdinASTEntry::Array(_, array) = &val.1[0] {
                let mut res = Vec::new();
                for (i, v) in array.iter().enumerate() {
                    if let OdinASTEntry::Value(None, v) = v {
                        res.push(V::deserialize(src, v).map_err(|e| format!("[{}]: {}", i, e))?);
                    }
                }
                Ok(Self {
                    contents: res,
                    ty: determine.0,
                    kind: determine.1,
                })
            } else {
                Err("List entry must be Array".to_string())
            }
        } else {
            Err("List type unrecognized".to_string())
        }
    }
}

impl<V: OdinSTSerializable> OdinSTSerializableRefType for OdinSTRefList<V> {
    fn serialize(&self, builder: &mut OdinASTBuilder) -> OdinASTStruct {
        let tpx = match self.kind {
            OdinSTRefListKind::Array => match self.ty.rfind(',') {
                Some(x) => format!("{}[]{}", &self.ty[..x], &self.ty[x..]),
                None => format!("{}[]", self.ty),
            },
            OdinSTRefListKind::List => {
                format!("System.Collections.Generic.List`1[[{}]], mscorlib", self.ty)
            }
        };
        let array_content: Vec<OdinASTEntry> = self
            .contents
            .iter()
            .map(|v| OdinASTEntry::Value(None, v.serialize(builder)))
            .collect();
        OdinASTStruct(
            Some(tpx),
            vec![OdinASTEntry::Array(
                self.contents.len() as i64,
                array_content,
            )],
        )
    }
}

/// RuntimeType as a convenient struct.
pub struct OdinSTRuntimeType(pub String);

impl OdinSTDeserializableRefType for OdinSTRuntimeType {
    fn deserialize(_src: &OdinASTRefMap, val: &OdinASTStruct) -> Result<Self, String> {
        let sct = val.unwrap_fixed_type("System.RuntimeType, mscorlib", 1)?;
        if let OdinASTEntry::Value(None, OdinASTValue::Primitive(OdinPrimitive::String(ty))) =
            &sct[0]
        {
            Ok(OdinSTRuntimeType(ty.clone()))
        } else {
            Err("RuntimeType should just contain an unnamed string".to_string())
        }
    }
}

impl OdinSTSerializable for OdinSTRuntimeType {
    fn serialize(&self, builder: &mut OdinASTBuilder) -> OdinASTValue {
        OdinASTValue::InternalRef(builder.runtime_type(&self.0))
    }
}

/// StrongBox as a convenient struct.
/// Note that V doesn't have any bounds here, because it might be either only serializable or deserializable.
pub struct OdinSTStrongBox<V>(pub String, pub V);

impl<V: OdinSTDeserializable> OdinSTDeserializableRefType for OdinSTStrongBox<V> {
    fn deserialize(src: &OdinASTRefMap, val: &OdinASTStruct) -> Result<Self, String> {
        if let Some(type_name) = &val.0 {
            let ty2 = type_name
                .strip_prefix("System.Runtime.CompilerServices.StrongBox`1[[")
                .and_then(|v| v.strip_suffix("]], System.Core"));
            if let Some(ty2) = ty2 {
                let val = OdinASTEntry::get_value_by_name("Value", &val.1)?;
                Ok(Self(
                    ty2.to_string(),
                    V::deserialize(src, val).map_err(|v| format!("StrongBox.Value: {}", v))?,
                ))
            } else {
                Err(format!("{} is not a StrongBox", type_name))
            }
        } else {
            Err("StrongBox needs type name to fully deserialize".to_string())
        }
    }
}

impl<V: OdinSTSerializable> OdinSTSerializableRefType for OdinSTStrongBox<V> {
    fn serialize(&self, builder: &mut OdinASTBuilder) -> OdinASTStruct {
        let emitted_val = self.1.serialize(builder);
        OdinASTStruct(
            Some(format!(
                "System.Runtime.CompilerServices.StrongBox`1[[{}]], System.Core",
                self.0
            )),
            vec![OdinASTEntry::nval("Value", emitted_val)],
        )
    }
}

// -- Utility Functions --

/// Shorthand to deserialize a field.
pub fn odinst_get_field<V: OdinSTDeserializable>(
    src: &OdinASTRefMap,
    content: &[OdinASTEntry],
    name: &str,
) -> Result<V, String> {
    let val = OdinASTEntry::get_value_by_name(name, content)?;
    V::deserialize(src, val).map_err(|v| format!("{}: {}", name, v))
}
