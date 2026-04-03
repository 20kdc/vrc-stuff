use kudonodin::*;

use std::collections::BTreeMap;

use serde::{Deserialize, Serialize};

#[derive(Clone, Debug, Serialize, Deserialize, PartialEq, Eq, PartialOrd, Ord)]
pub struct UdonRawSyncProperty(pub String, pub u64);

impl OdinSTSerializableRefType for UdonRawSyncProperty {
    fn serialize(&self, _builder: &mut kudonodin::OdinASTBuilder) -> kudonodin::OdinASTStruct {
        OdinASTStruct(
            Some("VRC.Udon.Common.UdonSyncProperty, VRC.Udon.Common".to_string()),
            vec![OdinASTEntry::Array(
                2,
                vec![
                    OdinASTEntry::nval("type", "System.String, mscorlib"),
                    OdinASTEntry::nval("Name", self.0.as_str()),
                    OdinASTEntry::nval(
                        "type",
                        "VRC.Udon.Common.Interfaces.UdonSyncInterpolationMethod, VRC.Udon.Common",
                    ),
                    OdinASTEntry::nval("InterpolationAlgorithm", OdinPrimitive::ULong(self.1)),
                ],
            )],
        )
    }
}

impl OdinSTDeserializableRefType for UdonRawSyncProperty {
    fn deserialize(src: &OdinASTFile, val: &OdinASTStruct) -> Result<Self, String> {
        let content = val.unwrap_iserializable()?;
        Ok(UdonRawSyncProperty(
            odinst_get_field(src, &content, "Name")?,
            odinst_get_field(src, &content, "InterpolationAlgorithm")?,
        ))
    }
}

#[derive(Clone, Debug, Serialize, Deserialize, PartialEq, Eq, PartialOrd, Ord)]
pub struct UdonRawSyncMetadata(pub String, pub Vec<UdonRawSyncProperty>);

impl OdinSTSerializableRefType for UdonRawSyncMetadata {
    fn serialize(&self, builder: &mut kudonodin::OdinASTBuilder) -> OdinASTStruct {
        let properties = OdinSTSerializable::serialize(
            &OdinSTRefList {
                contents: self.1.clone(),
                ty: "VRC.Udon.Common.Interfaces.IUdonSyncProperty, VRC.Udon.Common".to_string(),
                kind: OdinSTRefListKind::List,
            },
            builder,
        );
        OdinASTStruct(
            Some("VRC.Udon.Common.UdonSyncMetadata, VRC.Udon.Common".to_string()),
            vec![OdinASTEntry::Array(
                2,
                vec![
                    OdinASTEntry::nval("type", "System.String, mscorlib"),
                    OdinASTEntry::nval("Name", self.0.as_str()),
                    OdinASTEntry::nval(
                        "type",
                        "System.Collections.Generic.List`1[[VRC.Udon.Common.Interfaces.IUdonSyncProperty, VRC.Udon.Common]], mscorlib",
                    ),
                    OdinASTEntry::nval("Properties", properties),
                ],
            )],
        )
    }
}

impl OdinSTDeserializableRefType for UdonRawSyncMetadata {
    fn deserialize(src: &OdinASTFile, val: &OdinASTStruct) -> Result<Self, String> {
        let content = val.unwrap_iserializable()?;
        let reflist: OdinSTRefList<UdonRawSyncProperty> =
            odinst_get_field(src, &content, "Properties")?;
        Ok(UdonRawSyncMetadata(
            odinst_get_field(src, &content, "Name")?,
            reflist.contents,
        ))
    }
}

#[derive(Clone, Debug, Default, Serialize, Deserialize, PartialEq, Eq, PartialOrd, Ord)]
pub struct UdonRawSyncMetadataTable(pub Vec<UdonRawSyncMetadata>);

impl UdonRawSyncMetadataTable {
    pub fn from_flat(table: &[(String, String, u64)]) -> Self {
        let mut symbol_vec = Vec::new();

        // The basic idea is that we keep adding props to lists.
        // At the end (once all props are confirmed) we serialize.
        // Situations like this are why the Odin AST refs table was built the way it was.
        // However, the code was cumbersome, so now there's this whole chain-of-ASTs thing going on.
        // This has other problems, but it works.

        let mut symbol_prop_list_map: BTreeMap<String, Vec<UdonRawSyncProperty>> = BTreeMap::new();

        for (kname, kprop, v) in table {
            let built = UdonRawSyncProperty(kprop.clone(), *v);

            match symbol_prop_list_map.get_mut(kname) {
                Some(xm) => {
                    xm.push(built);
                }
                None => {
                    symbol_vec.push(kname.clone());
                    symbol_prop_list_map.insert(kname.clone(), vec![built]);
                }
            }
        }

        UdonRawSyncMetadataTable(
            symbol_vec
                .drain(..)
                .map(|sym_name| {
                    let res = symbol_prop_list_map.remove(&sym_name).unwrap();
                    UdonRawSyncMetadata(sym_name, res)
                })
                .collect(),
        )
    }
}

impl OdinSTSerializableRefType for UdonRawSyncMetadataTable {
    fn serialize(&self, builder: &mut kudonodin::OdinASTBuilder) -> OdinASTStruct {
        let meta = OdinSTSerializable::serialize(
            &OdinSTRefList {
                contents: self.0.clone(),
                ty: "VRC.Udon.Common.Interfaces.IUdonSyncMetadata, VRC.Udon.Common".to_string(),
                kind: OdinSTRefListKind::List,
            },
            builder,
        );
        OdinASTStruct(
            Some("VRC.Udon.Common.UdonSyncMetadataTable, VRC.Udon.Common".to_string()),
            vec![OdinASTEntry::Array(
                1,
                vec![
                    OdinASTEntry::nval(
                        "type",
                        "System.Collections.Generic.List`1[[VRC.Udon.Common.Interfaces.IUdonSyncMetadata, VRC.Udon.Common]], mscorlib",
                    ),
                    OdinASTEntry::nval("SyncMetadata", meta),
                ],
            )],
        )
    }
}

impl OdinSTDeserializableRefType for UdonRawSyncMetadataTable {
    fn deserialize(src: &OdinASTFile, val: &OdinASTStruct) -> Result<Self, String> {
        let content = val.unwrap_iserializable()?;
        let reflist: OdinSTRefList<UdonRawSyncMetadata> =
            odinst_get_field(src, &content, "SyncMetadata")?;
        Ok(Self(reflist.contents))
    }
}
