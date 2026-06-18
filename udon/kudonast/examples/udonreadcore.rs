fn main() {
    let mut args = std::env::args();
    _ = args.next();
    let filename = args.next().expect("filename arg must be passed");
    assert!(args.next().is_none());
    let res = std::fs::read(filename).expect("file must be readable");
    let core_dump: kudonast::UdonCoreDump =
        kudonodin::OdinSTDeserializable::deserialize_bytes(&res).expect("must deserialize");
    let pcfg = ron::ser::PrettyConfig::new().indentor("\t");
    println!(
        "{}",
        ron::ser::to_string_pretty(&core_dump, pcfg).expect("should translate properly")
    );
}
