fn main() {
    let mut args = std::env::args();
    _ = args.next();
    let filename = args.next().expect("filename arg must be passed");
    assert!(args.next().is_none());
    let res = std::fs::read(filename).expect("file must be readable");
    let entries = kudonodin::OdinEntry::read_all_from_slice(&res).expect("decode must succeed");
    let pcfg = ron::ser::PrettyConfig::new().indentor("\t");
    println!(
        "{}",
        ron::ser::to_string_pretty(&entries, pcfg).expect("should translate properly")
    );
}
