use crate::*;

#[test]
fn crosscheck() {
    // Binary extracted from Udon Graph program
    let true_odin_binary = include_bytes!("docExample.odin.bin");
    // Hand-written to match
    let ron = include_str!("docExample.ron");

    let udon_program: UdonProgram = ron::from_str(ron).expect("decode must succeed");
    let file = udonprogram_emit_odin(&udon_program).expect("assemble must succeed");

    let true_entries =
        kudonodin::OdinEntry::read_all_from_slice(true_odin_binary).expect("decode must succeed");
    let true_file = kudonodin::OdinASTFile::from_entries(true_entries);

    let pcfg = ron::ser::PrettyConfig::new().indentor("\t");

    let reference_ron =
        ron::ser::to_string_pretty(&true_file, pcfg.clone()).expect("should translate properly");
    let output_ron = ron::ser::to_string_pretty(&file.0, pcfg).expect("should translate properly");

    std::fs::write("crosscheck_ref.ron", &reference_ron).expect("write 1 should succeed");
    std::fs::write("crosscheck_out.ron", &output_ron).expect("write 2 should succeed");

    assert_eq!(&reference_ron, &output_ron);
}
