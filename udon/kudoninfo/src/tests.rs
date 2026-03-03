#[test]
pub fn test_udontype_lookups() {
    // These should always pass.
    // Since [udontype_map] is called, this guarantees all Udon types parsed.
    _ = crate::udontype_get("SystemInt32").unwrap();
    _ = crate::udontype_get("SystemUInt32").unwrap();
    _ = crate::udontype_get("VRCSDKBaseVRCRenderTexture").unwrap();
    // Do this to be sure the Udon extern map parses.
    _ = crate::udonextern_map();
}

#[test]
pub fn test_extern_attributes() {
    assert!(
        !crate::udonextern_get(
            "TMProTextMeshProUGUIArray.__SetValue__SystemObject_SystemInt32_SystemInt32__SystemVoid"
        )
        .unwrap()
        .method_static
    );
    assert_eq!(
        crate::udonextern_get(
            "TMProTextMeshProUGUIArray.__SetValue__SystemObject_SystemInt32_SystemInt32__SystemVoid"
        )
        .unwrap()
        .name_parsed
        .return_type,
        "SystemVoid"
    );
    assert!(
        crate::udonextern_get("SystemByte.__op_Addition__SystemByte_SystemByte__SystemInt32")
            .unwrap()
            .method_static
    );
}
