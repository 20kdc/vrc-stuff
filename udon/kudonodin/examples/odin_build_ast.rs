fn main() {
    let mut args = std::env::args();
    _ = args.next();
    let filename = args.next().expect("filename arg must be passed");
    assert!(args.next().is_none());
    let res: kudonodin::OdinASTFile =
        ron::de::from_reader(std::io::stdin()).expect("RON must parse");
    let res2 = res.to_entry_vec();
    let total: Vec<u8> = kudonodin::OdinEntry::write_all_to_bytes(&res2);
    std::fs::write(filename, total).expect("writing to file should succeed");
}
