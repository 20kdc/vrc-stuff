//! Simple 'deserializable' framework for translation from AST form to more concrete Rust objects.

use crate::*;

/// Deserializable.
pub trait OdinSTDeserializable: Sized {
    /// Serializes into the given builder.
    fn deserialize(src: &OdinASTFile, val: &OdinASTValue) -> Result<Self, String>;
}

/// Shorthand to deserialize a field.
pub fn odinst_get_field<V: OdinSTDeserializable>(src: &OdinASTFile, content: &[OdinASTEntry], name: &str) -> Result<V, String> {
    let val = OdinASTEntry::get_value_by_name(name, content)?;
    V::deserialize(src, val).map_err(|v| format!("{}: {}", name, v))
}

impl OdinSTDeserializable for OdinASTValue {
    fn deserialize(_src: &OdinASTFile, val: &OdinASTValue) -> Result<Self, String> {
        Ok(val.clone())
    }
}

impl OdinSTDeserializable for OdinPrimitive {
    fn deserialize(_src: &OdinASTFile, val: &OdinASTValue) -> Result<Self, String> {
        if let OdinASTValue::Primitive(prim) = val {
            Ok(prim.clone())
        } else {
            Err("Expected primitive".to_string())
        }
    }
}

macro_rules! serializable_int_impl {
    ($type:ty, $oit:expr, $arraytype:expr, $pat:ident, $type_pa:ty) => {
        impl OdinSTDeserializable for $type {
            fn deserialize(_src: &OdinASTFile, val: &OdinASTValue) -> Result<Self, String> {
                if let OdinASTValue::Primitive(prim) = val {
                    if let Some(v) = prim.decompose_int() {
                        Ok(v.1 as $type)
                    } else {
                        Err("Expected integer of some kind".to_string())
                    }
                } else {
                    Err("Expected integer of some kind".to_string())
                }
            }
        }
        impl OdinSTDeserializableRefType for Vec<$type> {
            fn deserialize(_src: &OdinASTFile, val: &OdinASTStruct) -> Result<Self, String> {
                let v = val.unwrap_fixed_type($arraytype, 1)?;
                if let OdinASTEntry::PrimitiveArray(OdinPrimitiveArray::$pat(v)) = &v[0] {
                    Ok(v.iter().map(|v| *v as $type).collect())
                } else {
                    Err("Expected primitive array".to_string())
                }
            }
        }
    };
}

serializable_int_impl!(
    u64,
    OdinIntType::ULong,
    "System.UInt64[], mscorlib",
    U64,
    u64
);
serializable_int_impl!(i64, OdinIntType::Long, "System.Int64[], mscorlib", U64, u64);
serializable_int_impl!(u32, OdinIntType::Int, "System.UInt32[], mscorlib", U32, u32);
serializable_int_impl!(
    i32,
    OdinIntType::UInt,
    "System.Int32[], mscorlib",
    U32,
    u32
);
serializable_int_impl!(
    u16,
    OdinIntType::UShort,
    "System.UInt16[], mscorlib",
    U16,
    u16
);
serializable_int_impl!(
    i16,
    OdinIntType::Short,
    "System.Int16[], mscorlib",
    U16,
    u16
);
serializable_int_impl!(u8, OdinIntType::Byte, "System.Byte[], mscorlib", U8, u8);
serializable_int_impl!(i8, OdinIntType::SByte, "System.SByte[], mscorlib", U8, u8);

impl OdinSTDeserializable for f64 {
    fn deserialize(_src: &OdinASTFile, val: &OdinASTValue) -> Result<Self, String> {
        if let OdinASTValue::Primitive(prim) = val {
            if let OdinPrimitive::Double(v) = prim {
                Ok(*v)
            } else if let OdinPrimitive::Float(v) = prim {
                Ok(*v as f64)
            } else if let Some((_, b)) = prim.decompose_int() {
                Ok(b as f64)
            } else {
                Err("f64: incompatible primitive".to_string())
            }
        } else {
            Err("f64: non-primitive".to_string())
        }
    }
}

impl OdinSTDeserializable for f32 {
    fn deserialize(_src: &OdinASTFile, val: &OdinASTValue) -> Result<Self, String> {
        if let OdinASTValue::Primitive(prim) = val {
            if let OdinPrimitive::Double(v) = prim {
                Ok(*v as f32)
            } else if let OdinPrimitive::Float(v) = prim {
                Ok(*v)
            } else if let Some((_, b)) = prim.decompose_int() {
                Ok(b as f32)
            } else {
                Err("f32: incompatible primitive".to_string())
            }
        } else {
            Err("f32: non-primitive".to_string())
        }
    }
}

impl OdinSTDeserializable for String {
    fn deserialize(_src: &OdinASTFile, val: &OdinASTValue) -> Result<Self, String> {
        if let OdinASTValue::Primitive(OdinPrimitive::String(s)) = val {
            Ok(s.clone())
        } else {
            Err("String: expected string".to_string())
        }
    }
}

/// Implements the same basic stuff for reference types.
pub trait OdinSTDeserializableRefType: Sized {
    fn deserialize(src: &OdinASTFile, val: &OdinASTStruct) -> Result<Self, String>;
}

impl<T: OdinSTDeserializableRefType> OdinSTDeserializable for T {
    fn deserialize(src: &OdinASTFile, val: &OdinASTValue) -> Result<Self, String> {
        if let OdinASTValue::InternalRef(i) = val {
            if let Some(v) = src.refs.get(i) {
                OdinSTDeserializableRefType::deserialize(src, v)
            } else {
                Err(format!("InternalRef {} missing", i))
            }
        } else {
            Err("InternalRef expected".to_string())
        }
    }
}

impl<T: OdinSTDeserializable> OdinSTDeserializable for Option<T> {
    fn deserialize(src: &OdinASTFile, val: &OdinASTValue) -> Result<Self, String> {
        if let OdinASTValue::Primitive(OdinPrimitive::Null) = val {
            Ok(None)
        } else {
            Ok(Some(OdinSTDeserializable::deserialize(src, val)?))
        }
    }
}

/// Wrapper for lists of reference types.
/// Automatically adapts between arrays and lists using the 'don't check' technique.
pub struct OdinSTRefList<V>(pub Vec<V>);

impl<V: OdinSTDeserializable> OdinSTDeserializableRefType for OdinSTRefList<V> {
    fn deserialize(src: &OdinASTFile, val: &OdinASTStruct) -> Result<Self, String> {
        if val.1.len() != 1 {
            Err("List must have one (Array) entry".to_string())
        } else if let OdinASTEntry::Array(_, array) = &val.1[0] {
            let mut res = Vec::new();
            for (i, v) in array.iter().enumerate() {
                if let OdinASTEntry::Value(None, v) = v {
                    res.push(V::deserialize(src, v).map_err(|e| format!("[{}]: {}", i, e))?);
                }
            }
            Ok(Self(res))
        } else {
            Err("List entry must be Array".to_string())
        }
    }
}

/// RuntimeType as a convenient struct.
pub struct OdinSTRuntimeType(pub String);

impl OdinSTDeserializableRefType for OdinSTRuntimeType {
    fn deserialize(_src: &OdinASTFile, val: &OdinASTStruct) -> Result<Self, String> {
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

/// StrongBox as a convenient struct.
/// Note that V doesn't have any bounds here, because it might be either only serializable or deserializable.
pub struct OdinSTStrongBox<V>(pub String, pub V);

impl<V: OdinSTDeserializable> OdinSTDeserializableRefType for OdinSTStrongBox<V> {
    fn deserialize(src: &OdinASTFile, val: &OdinASTStruct) -> Result<Self, String> {
        if let Some(type_name) = &val.0 {
            let ty2 = type_name.strip_prefix("System.Runtime.CompilerServices.StrongBox`1[[")
                .and_then(|v| v.strip_suffix("]], System.Core"));
            if let Some(ty2) = ty2 {
                let val = OdinASTEntry::get_value_by_name("Value", &val.1)?;
                Ok(Self(ty2.to_string(), V::deserialize(src, val).map_err(|v| format!("StrongBox.Value: {}", v))?))
            } else {
                Err(format!("{} is not a StrongBox", type_name))
            }
        } else {
            Err("StrongBox needs type name to fully deserialize".to_string())
        }
    }
}
