fn main() {
    let mut args = std::env::args();
    _ = args.next();
    let filename = args.next().expect("filename arg must be passed");
    let mode = args.next().expect("mode arg must be passed (udonjson/odinast/uasm)");
    assert!(args.next().is_none());
    let res = std::fs::read_to_string(filename).expect("file must be readable");
    let udon_program: kudonast::UdonProgram = ron::from_str(&res).expect("decode must succeed");
    if mode.eq("udonjson") {
        let file = kudonast::udonprogram_emit_udonjson(&udon_program).expect("assemble must succeed");
        println!(
            "{}",
            file.dump()
        );
    } else if mode.eq("odinast") {
        let file = kudonast::udonprogram_emit_odin(&udon_program).expect("assemble must succeed");
        let pcfg = ron::ser::PrettyConfig::new().indentor("\t");
        println!(
            "{}",
            ron::ser::to_string_pretty(&file, pcfg).expect("should translate properly")
        );
    } else if mode.eq("uasm") {
        let uasm_writer = kudonast::UASMWriter::default();
        // Errors are thrown away on purpose. :(
        _ = kudonast::udonprogram_emit_uasm(&udon_program, &uasm_writer);
        println!(
            "{}",
            uasm_writer
        );
    } else {
        panic!("acceptable modes: udonjson/odinast/uasm");
    }
}
