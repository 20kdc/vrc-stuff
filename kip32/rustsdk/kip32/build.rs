use std::path::PathBuf;
fn main() {
    let out = &PathBuf::from(std::env::var_os("OUT_DIR").unwrap());
    let kip32ld = include_bytes!("../../sdk/kip32.ld");

    std::fs::write(out.join("link.x"), kip32ld).unwrap();

    // println!("cargo:rustc-link-arg=-Wl,-Tlink.x"); // doesn't work
    println!("cargo:rustc-link-search={}", out.display());
    println!("cargo:rerun-if-changed=../../sdk/kip32.ld");
}
