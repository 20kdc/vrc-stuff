//! Simple 'deserializable' framework for translation from AST form to more concrete Rust objects.

use crate::*;

// -- Core Traits --

/// Deserializable.
pub trait OdinSTDeserializable: Sized {
    /// Serializes into the given builder.
    fn deserialize(src: &OdinASTFile, val: &OdinASTValue) -> Result<Self, String>;
}

/// Serializable.
/// It is generally a good idea to be careful with ordering here.
/// OdinSerializer serializes in a depth-first manner.
pub trait OdinSTSerializable {
    fn serialize(&self, builder: &mut OdinASTBuilder) -> OdinASTValue;
}

/// Implements [OdinSTDeserializable] for reference types.
/// Acts as a convenience layer.
pub trait OdinSTDeserializableRefType: Sized {
    fn deserialize(src: &OdinASTFile, val: &OdinASTStruct) -> Result<Self, String>;
}

/// Implements [OdinSTSerializable] for reference types.
/// Acts as a convenience layer.
pub trait OdinSTSerializableRefType {
    fn serialize(&self, builder: &mut OdinASTBuilder) -> OdinASTStruct;
}

// -- Fundamental Trait Impls --

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

impl<T: OdinSTSerializableRefType> OdinSTSerializable for T {
    fn serialize(&self, builder: &mut OdinASTBuilder) -> OdinASTValue {
        // We allocate the ID 'at the start of the object', like we would if were writing this in-order.
        let refid = builder.alloc_refid();
        let content = OdinSTSerializableRefType::serialize(self, builder);
        builder.file.refs.insert(refid, content);
        OdinASTValue::InternalRef(refid)
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

// -- Trivial Equivalences (OdinASTValue & OdinPrimitive) --

impl OdinSTDeserializable for OdinASTValue {
    fn deserialize(_src: &OdinASTFile, val: &OdinASTValue) -> Result<Self, String> {
        Ok(val.clone())
    }
}

impl OdinSTSerializable for OdinASTValue {
    fn serialize(&self, _builder: &mut OdinASTBuilder) -> OdinASTValue {
        self.clone()
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

impl OdinSTSerializable for OdinPrimitive {
    fn serialize(&self, _builder: &mut OdinASTBuilder) -> OdinASTValue {
        OdinASTValue::Primitive(self.clone())
    }
}

// -- Integers --

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
        impl OdinSTSerializable for $type {
            fn serialize(&self, _builder: &mut OdinASTBuilder) -> OdinASTValue {
                OdinASTValue::Primitive(OdinPrimitive::compose_int($oit, *self as i64))
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
        impl OdinSTSerializableRefType for Vec<$type> {
            fn serialize(&self, _builder: &mut OdinASTBuilder) -> OdinASTStruct {
                let v = self.iter().map(|v| *v as $type_pa).collect();
                OdinASTStruct(
                    Some($arraytype.to_string()),
                    vec![OdinASTEntry::PrimitiveArray(OdinPrimitiveArray::$pat(v))],
                )
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
serializable_int_impl!(i32, OdinIntType::UInt, "System.Int32[], mscorlib", U32, u32);
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

// -- Floats --

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

impl OdinSTSerializable for f64 {
    fn serialize(&self, _builder: &mut OdinASTBuilder) -> OdinASTValue {
        OdinASTValue::Primitive(OdinPrimitive::Double(*self))
    }
}

impl OdinSTDeserializableRefType for Vec<f64> {
    fn deserialize(_src: &OdinASTFile, val: &OdinASTStruct) -> Result<Self, String> {
        let v = val.unwrap_fixed_type("System.Double[], mscorlib", 1)?;
        if let OdinASTEntry::PrimitiveArray(OdinPrimitiveArray::U64(v)) = &v[0] {
            Ok(v.iter().map(|v| f64::from_bits(*v)).collect())
        } else {
            Err("Expected primitive array".to_string())
        }
    }
}

impl OdinSTSerializableRefType for Vec<f64> {
    fn serialize(&self, _builder: &mut OdinASTBuilder) -> OdinASTStruct {
        let v = self.iter().map(|v| v.to_bits()).collect();
        OdinASTStruct(
            Some("System.Double[], mscorlib".to_string()),
            vec![OdinASTEntry::PrimitiveArray(OdinPrimitiveArray::U64(v))],
        )
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

impl OdinSTSerializable for f32 {
    fn serialize(&self, _builder: &mut OdinASTBuilder) -> OdinASTValue {
        OdinASTValue::Primitive(OdinPrimitive::Float(*self))
    }
}

impl OdinSTDeserializableRefType for Vec<f32> {
    fn deserialize(_src: &OdinASTFile, val: &OdinASTStruct) -> Result<Self, String> {
        let v = val.unwrap_fixed_type("System.Double[], mscorlib", 1)?;
        if let OdinASTEntry::PrimitiveArray(OdinPrimitiveArray::U32(v)) = &v[0] {
            Ok(v.iter().map(|v| f32::from_bits(*v)).collect())
        } else {
            Err("Expected primitive array".to_string())
        }
    }
}

impl OdinSTSerializableRefType for Vec<f32> {
    fn serialize(&self, _builder: &mut OdinASTBuilder) -> OdinASTStruct {
        let v = self.iter().map(|v| v.to_bits()).collect();
        OdinASTStruct(
            Some("System.Double[], mscorlib".to_string()),
            vec![OdinASTEntry::PrimitiveArray(OdinPrimitiveArray::U32(v))],
        )
    }
}

// -- Bool --

impl OdinSTDeserializable for bool {
    fn deserialize(_src: &OdinASTFile, val: &OdinASTValue) -> Result<Self, String> {
        if let OdinASTValue::Primitive(prim) = val {
            if let Some((_, b)) = prim.decompose_int() {
                Ok(b != 0)
            } else {
                Err("bool: incompatible primitive".to_string())
            }
        } else {
            Err("bool: non-primitive".to_string())
        }
    }
}

impl OdinSTSerializable for bool {
    fn serialize(&self, _builder: &mut OdinASTBuilder) -> OdinASTValue {
        OdinASTValue::Primitive(OdinPrimitive::Bool(*self))
    }
}

impl OdinSTDeserializableRefType for Vec<bool> {
    fn deserialize(_src: &OdinASTFile, val: &OdinASTStruct) -> Result<Self, String> {
        let v = val.unwrap_fixed_type("System.Boolean[], mscorlib", 1)?;
        if let OdinASTEntry::PrimitiveArray(OdinPrimitiveArray::U8(v)) = &v[0] {
            Ok(v.iter().map(|v| *v != 0).collect())
        } else {
            Err("Expected primitive array".to_string())
        }
    }
}

impl OdinSTSerializableRefType for Vec<bool> {
    fn serialize(&self, _builder: &mut OdinASTBuilder) -> OdinASTStruct {
        let v = self.iter().map(|v| if *v { 1 } else { 0 }).collect();
        OdinASTStruct(
            Some("System.Boolean[], mscorlib".to_string()),
            vec![OdinASTEntry::PrimitiveArray(OdinPrimitiveArray::U8(v))],
        )
    }
}

// -- Char --

#[derive(Clone, Copy, Debug, Deserialize, Serialize, PartialEq, Eq, PartialOrd, Ord)]
pub struct OdinSTChar(pub u16);

impl OdinSTDeserializable for OdinSTChar {
    fn deserialize(_src: &OdinASTFile, val: &OdinASTValue) -> Result<Self, String> {
        if let OdinASTValue::Primitive(prim) = val {
            if let Some((_, b)) = prim.decompose_int() {
                Ok(OdinSTChar(b as u16))
            } else {
                Err("bool: incompatible primitive".to_string())
            }
        } else {
            Err("bool: non-primitive".to_string())
        }
    }
}

impl OdinSTSerializable for OdinSTChar {
    fn serialize(&self, _builder: &mut OdinASTBuilder) -> OdinASTValue {
        OdinASTValue::Primitive(OdinPrimitive::Char(self.0))
    }
}

impl OdinSTDeserializableRefType for Vec<OdinSTChar> {
    fn deserialize(_src: &OdinASTFile, val: &OdinASTStruct) -> Result<Self, String> {
        let v = val.unwrap_fixed_type("System.Boolean[], mscorlib", 1)?;
        if let OdinASTEntry::PrimitiveArray(OdinPrimitiveArray::U16(v)) = &v[0] {
            Ok(v.iter().map(|v| OdinSTChar(*v)).collect())
        } else {
            Err("Expected primitive array".to_string())
        }
    }
}

impl OdinSTSerializableRefType for Vec<OdinSTChar> {
    fn serialize(&self, _builder: &mut OdinASTBuilder) -> OdinASTStruct {
        let v = self.iter().map(|v| v.0).collect();
        OdinASTStruct(
            Some("System.Boolean[], mscorlib".to_string()),
            vec![OdinASTEntry::PrimitiveArray(OdinPrimitiveArray::U16(v))],
        )
    }
}

// -- String --

impl OdinSTDeserializable for String {
    fn deserialize(_src: &OdinASTFile, val: &OdinASTValue) -> Result<Self, String> {
        if let OdinASTValue::Primitive(OdinPrimitive::String(s)) = val {
            Ok(s.clone())
        } else {
            Err("String: expected string".to_string())
        }
    }
}

impl OdinSTSerializable for String {
    fn serialize(&self, _builder: &mut OdinASTBuilder) -> OdinASTValue {
        OdinASTValue::Primitive(OdinPrimitive::String(self.clone()))
    }
}
