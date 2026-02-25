#[test]
fn test_roundtrip() {
    let test_data = include_bytes!("helloWorld.odin.bin");
    let read_all = super::OdinEntry::read_all_from_slice(test_data).expect("should read properly");
    let round_trip = super::OdinEntry::write_all_to_bytes(&read_all);
    assert_eq!(test_data.as_slice(), &round_trip);
}

#[test]
fn test_ast_roundtrip() {
    let test_data = include_bytes!("helloWorld.odin.bin");
    let read_all = super::OdinEntry::read_all_from_slice(test_data).expect("should read properly");
    let round_trip = super::OdinASTFile::from_entries(read_all.clone()).to_entry_vec();
    assert_eq!(read_all.len(), round_trip.len());
    for i in 0..read_all.len() {
        assert_eq!(read_all[i], round_trip[i]);
    }
}
