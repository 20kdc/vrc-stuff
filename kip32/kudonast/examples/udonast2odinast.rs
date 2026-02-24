fn main() {
    let mut args = std::env::args();
    _ = args.next();
    let filename = args.next().expect("filename arg must be passed");
    assert!(args.next().is_none());
    let res = std::fs::read_to_string(filename).expect("file must be readable");
    let udon_program: kudonast::UdonProgram = ron::from_str(&res).expect("decode must succeed");
    let file = kudonast::udonprogram_emit_odin(&udon_program).expect("assemble must succeed");
    let pcfg = ron::ser::PrettyConfig::new().indentor("\t");
    println!(
        "{}",
        ron::ser::to_string_pretty(&file, pcfg).expect("should translate properly")
    );
}
