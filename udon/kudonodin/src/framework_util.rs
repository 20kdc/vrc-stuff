use crate::*;

// -- .NET Types --

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

impl<T: OdinSTDeserializable> OdinSTDeserializable for Option<T> {
    fn deserialize(src: &OdinASTFile, val: &OdinASTValue) -> Result<Self, String> {
        if let OdinASTValue::Primitive(OdinPrimitive::Null) = val {
            Ok(None)
        } else {
            Ok(Some(OdinSTDeserializable::deserialize(src, val)?))
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

impl OdinSTSerializable for OdinSTRuntimeType {
    fn serialize(&self, builder: &mut OdinASTBuilder) -> OdinASTValue {
        OdinASTValue::InternalRef(builder.runtime_type(&self.0))
    }
}

/// StrongBox as a convenient struct.
/// Note that V doesn't have any bounds here, because it might be either only serializable or deserializable.
pub struct OdinSTStrongBox<V>(pub String, pub V);

impl<V: OdinSTDeserializable> OdinSTDeserializableRefType for OdinSTStrongBox<V> {
    fn deserialize(src: &OdinASTFile, val: &OdinASTStruct) -> Result<Self, String> {
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
        OdinASTStruct(Some(format!("System.Runtime.CompilerServices.StrongBox`1[[{}]], System.Core", self.0)), vec![
            OdinASTEntry::nval("Value", emitted_val)
        ])
    }
}

// -- Utility Functions --

/// Shorthand to deserialize a field.
pub fn odinst_get_field<V: OdinSTDeserializable>(
    src: &OdinASTFile,
    content: &[OdinASTEntry],
    name: &str,
) -> Result<V, String> {
    let val = OdinASTEntry::get_value_by_name(name, content)?;
    V::deserialize(src, val).map_err(|v| format!("{}: {}", name, v))
}
