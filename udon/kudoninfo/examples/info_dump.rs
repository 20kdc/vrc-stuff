use kudoninfo::{UdonExtern, UdonType, udonextern_map, udontype_map};
use serde::Serialize;
use std::collections::BTreeMap;

#[derive(Serialize)]
struct DumpMii {
    pub types: &'static BTreeMap<String, UdonType>,
    pub externs: &'static BTreeMap<String, UdonExtern>,
}

fn main() {
    let file = DumpMii {
        types: udontype_map(),
        externs: udonextern_map(),
    };
    let pcfg = ron::ser::PrettyConfig::new().indentor("\t");
    println!(
        "{}",
        ron::ser::to_string_pretty(&file, pcfg).expect("should translate properly")
    );
}
