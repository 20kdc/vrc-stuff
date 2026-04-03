use kudonodin::{
    OdinASTEntry, OdinASTStruct, OdinPrimitive, OdinSTRefList, OdinSTRefListKind,
    OdinSTSerializable, OdinSTSerializableRefType,
};

use serde::{Deserialize, Serialize};

#[derive(Clone, Serialize, Deserialize, PartialEq, Eq, PartialOrd, Ord)]
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

#[derive(Clone, Serialize, Deserialize, PartialEq, Eq, PartialOrd, Ord)]
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

#[derive(Clone, Serialize, Deserialize, PartialEq, Eq, PartialOrd, Ord)]
pub struct UdonRawSyncMetadataTable(pub Vec<UdonRawSyncMetadata>);

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
