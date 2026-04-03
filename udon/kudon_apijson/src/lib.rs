use std::io::Read;
use std::sync::OnceLock;
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

static REF_VALUE: OnceLock<json::JsonValue> = OnceLock::new();

/// Reads the datamine to a JSON value.
/// This is cached, so you can call this as often as you want.
pub fn as_json() -> &'static json::JsonValue {
    REF_VALUE.get_or_init(|| json::parse(&as_text()).expect("api_c.json.xz should parse"))
}

/// Returns type names in an arbitrary order.
pub fn type_names() -> Vec<String> {
    let ty = &as_json()["types"];
    let mut res = Vec::new();
    for v in ty.entries() {
        res.push(v.0.to_string());
    }
    res
}

/// Gets the JSON object describing an Udon type, if it exists.
pub fn type_by_name(ty: &str) -> Option<&'static json::JsonValue> {
    let ty = &as_json()["types"][ty];
    if ty.is_object() { Some(ty) } else { None }
}

/// Gets contents of the base types field.
/// Notably, the bases field is already recursive, but does not include self.
pub fn type_bases(val: &json::JsonValue) -> Vec<&str> {
    let mut vec = Vec::new();
    for v in val["bases"].members() {
        vec.push(v.as_str().unwrap());
    }
    vec
}

/// Gets recursive bases.
pub fn type_bases_and_self<'lt>(ty: &'lt str, val: &'lt json::JsonValue) -> Vec<&'lt str> {
    let mut res = type_bases(val);
    assert!(!res.contains(&ty));
    res.push(ty);
    res
}
