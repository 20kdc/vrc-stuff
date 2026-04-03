use kudonodin::{
    OdinASTEntry, OdinASTStruct, OdinASTValue, OdinPrimitive, OdinSTSerializableRefType,
};

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

/// This temporarily uses OdinASTValue as a placeholder for the main sync property list.
pub struct UdonRawSyncMetadata(pub String, pub OdinASTValue);
impl OdinSTSerializableRefType for UdonRawSyncMetadata {
    fn serialize(&self, _builder: &mut kudonodin::OdinASTBuilder) -> OdinASTStruct {
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
                    OdinASTEntry::nval("Properties", self.1.clone()),
                ],
            )],
        )
    }
}

/// This temporarily uses OdinASTValue as a placeholder for the main IUdonSyncMetadata list.
pub struct UdonRawSyncMetadataTable(pub OdinASTValue);

impl OdinSTSerializableRefType for UdonRawSyncMetadataTable {
    fn serialize(&self, _builder: &mut kudonodin::OdinASTBuilder) -> OdinASTStruct {
        OdinASTStruct(
            Some("VRC.Udon.Common.UdonSyncMetadataTable, VRC.Udon.Common".to_string()),
            vec![OdinASTEntry::Array(
                1,
                vec![
                    OdinASTEntry::nval(
                        "type",
                        "System.Collections.Generic.List`1[[VRC.Udon.Common.Interfaces.IUdonSyncMetadata, VRC.Udon.Common]], mscorlib",
                    ),
                    OdinASTEntry::nval("SyncMetadata", self.0.clone()),
                ],
            )],
        )
    }
}
