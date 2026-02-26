use std::io::Read;
use xz::read::XzDecoder;

pub static DATA_XZ: &'static [u8] = include_bytes!("api_c.json.xz");

/// Reads the datamine to a string.
pub fn as_text() -> String {
    let mut decoder = XzDecoder::new(DATA_XZ);
    let mut s = String::new();
    decoder
        .read_to_string(&mut s)
        .expect("api_c.json.xz should properly decode");
    s
}

/// Reads the datamine to a JSON value.
pub fn as_json() -> json::JsonValue {
    json::parse(&as_text()).expect("api_c.json.xz should parse")
}
