use kudonodin::*;
use serde::{Deserialize, Serialize};

/// Raw Symbol. This pretty directly maps to the UdonSymbol type.
#[derive(Clone, Debug, Serialize, Deserialize, Default)]
pub struct UdonRawSymbol {
    pub name: String,
    pub ty: Option<String>,
    pub address: u32,
}

impl OdinSTDeserializableRefType for UdonRawSymbol {
    fn deserialize(src: &OdinASTRefMap, val: &OdinASTStruct) -> Result<Self, String> {
        let content = val.unwrap_iserializable()?;
        let name: String = odinst_get_field(src, content, "Name")?;
        let ty: Option<OdinSTRuntimeType> = odinst_get_field(src, content, "Type").ok();
        let address: u32 = odinst_get_field(src, content, "Address")?;
        Ok(UdonRawSymbol {
            name,
            ty: ty.map(|v| v.0),
            address,
        })
    }
}

impl OdinSTSerializableRefType for UdonRawSymbol {
    fn serialize(&self, builder: &mut OdinASTBuilder) -> OdinASTStruct {
        let mut typetype = OdinASTEntry::nval("type", "System.Object, mscorlib");
        let mut typeval = OdinASTEntry::nval("Type", OdinPrimitive::Null);
        if let Some(ty) = &self.ty {
            typetype = OdinASTEntry::nval("type", "System.RuntimeType, mscorlib");
            typeval =
                OdinASTEntry::nval("Type", OdinASTValue::InternalRef(builder.runtime_type(ty)));
        }
        OdinASTStruct(
            Some("VRC.Udon.Common.UdonSymbol, VRC.Udon.Common".to_string()),
            vec![OdinASTEntry::Array(
                3,
                vec![
                    OdinASTEntry::nval("type", "System.String, mscorlib"),
                    OdinASTEntry::nval("Name", self.name.as_str()),
                    typetype,
                    typeval,
                    OdinASTEntry::nval("type", "System.UInt32, mscorlib"),
                    OdinASTEntry::nval(
                        "Address",
                        OdinASTValue::Primitive(OdinPrimitive::UInt(self.address)),
                    ),
                ],
            )],
        )
    }
}

/// Raw Symbol Table. This pretty directly maps to the UdonSymbolTable type.
#[derive(Clone, Debug, Serialize, Deserialize, Default)]
pub struct UdonRawSymbolTable {
    pub symbols: Vec<UdonRawSymbol>,
    pub exported_symbols: Vec<String>,
}

impl OdinSTDeserializableRefType for UdonRawSymbolTable {
    fn deserialize(src: &OdinASTRefMap, val: &OdinASTStruct) -> Result<Self, String> {
        let content = val.unwrap_iserializable()?;
        let syms: OdinSTRefList<UdonRawSymbol> = odinst_get_field(src, content, "Symbols")?;
        let exported: OdinSTRefList<String> = odinst_get_field(src, content, "ExportedSymbols")?;
        Ok(Self {
            symbols: syms.contents,
            exported_symbols: exported.contents,
        })
    }
}

impl OdinSTSerializableRefType for UdonRawSymbolTable {
    fn serialize(&self, builder: &mut OdinASTBuilder) -> OdinASTStruct {
        let symbol_list = OdinSTSerializable::serialize(
            &OdinSTRefList {
                contents: self.symbols.clone(),
                ty: "VRC.Udon.Common.Interfaces.IUdonSymbol, VRC.Udon.Common".to_string(),
                kind: OdinSTRefListKind::List,
            },
            builder,
        );

        let export_list = OdinSTSerializable::serialize(
            &OdinSTRefList {
                contents: self.exported_symbols.clone(),
                ty: "System.String, mscorlib".to_string(),
                kind: OdinSTRefListKind::List,
            },
            builder,
        );

        OdinASTStruct(
            Some("VRC.Udon.Common.UdonSymbolTable, VRC.Udon.Common".to_string()),
            vec![OdinASTEntry::Array(
                2,
                vec![
                    OdinASTEntry::nval(
                        "type",
                        "System.Collections.Generic.List`1[[VRC.Udon.Common.Interfaces.IUdonSymbol, VRC.Udon.Common]], mscorlib",
                    ),
                    OdinASTEntry::nval("Symbols", symbol_list),
                    OdinASTEntry::nval(
                        "type",
                        "System.Collections.Generic.List`1[[System.String, mscorlib]], mscorlib",
                    ),
                    OdinASTEntry::nval("ExportedSymbols", export_list),
                ],
            )],
        )
    }
}

impl UdonRawSymbolTable {
    /// Maps a symbol to an address.
    /// Includes non-exported symbols.
    pub fn sym_to_addr(&self, s: &str) -> Option<u32> {
        for v in &self.symbols {
            if v.name.eq(s) {
                return Some(v.address);
            }
        }
        None
    }
}
