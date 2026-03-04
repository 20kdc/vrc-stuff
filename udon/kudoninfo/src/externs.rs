use crate::{
    UdonDBEntry, UdonDBRef, UdonTypeRef, udondbentry_impl, udondbref_getters, udontype_maxlen,
    udontyperef_get,
};
use serde::{Deserialize, Serialize};
use std::borrow::Cow;
use std::collections::BTreeMap;
use std::sync::OnceLock;

/// Used to parse out data from the Udon extern ID itself.
#[derive(Clone, Debug, PartialEq, Eq, PartialOrd, Ord, Serialize, Deserialize)]
pub struct UdonExternIDParse {
    pub wrapper_name: Cow<'static, str>,
    pub method_name: Cow<'static, str>,
    pub parameters: Cow<'static, [Cow<'static, str>]>,
    pub return_type: Cow<'static, str>,
}

impl UdonExternIDParse {
    pub fn parse(src: &str) -> Result<Self, String> {
        let wm: Vec<&str> = src.split(".").collect();
        if wm.len() != 2 {
            return Err(format!("Must have exactly one '.': {}", src));
        }
        let wrapper_name = wm[0].to_string();
        let presfx = wm[1]
            .strip_prefix("__")
            .ok_or_else(|| format!("Method name no '__' prefix: {}", src))?;
        let method_name_split = presfx
            .find("__")
            .ok_or_else(|| format!("Method name no '__' suffix: {}", src))?;
        let method_name = &presfx[..method_name_split];
        let mut remainder = &presfx[method_name_split + 2..];
        let mut parameters: Vec<Cow<'static, str>> = Vec::new();
        // only perform parameter search if remainder actually contains such a divider
        // if it doesn't (zero-parameter method) then everything is the return type
        if remainder.contains("__") {
            // remainder is a string of the form:
            // TMP_Dropdown_SystemInt32__SystemVoid
            // we have two strategies at our disposal, type database matching and underscore yolo
            // type database matching ensures we catch TMP classes
            // underscore yolo ensures we catch generics
            while remainder.len() > 0 {
                // found return type!
                if let Some(pfx) = remainder.strip_prefix("__") {
                    remainder = pfx;
                    break;
                } else {
                    // if it wasn't __, then we need to remove _
                    // which might not exist, because this might be the first arg
                    if let Some(pfx) = remainder.strip_prefix("_") {
                        remainder = pfx;
                    }
                    // type database matching
                    // notably we need to leave room for an underscore
                    let mut type_guard = udontype_maxlen().min(remainder.len() - 1);
                    while type_guard > 0 {
                        if udontyperef_get(&remainder[..type_guard]).is_some() {
                            // since suffixes can crop up, type database matching only provides a guard
                            break;
                        }
                        type_guard -= 1;
                    }
                    // underscore yolo
                    // notably, we already removed any underscore at the start
                    // we ignore underscores that appear before the guard set by type database matching (if any)
                    if let Some(mut underscore) = remainder[type_guard..].find("_") {
                        underscore += type_guard;
                        parameters.push(Cow::Owned(remainder[..underscore].to_string()));
                        remainder = &remainder[underscore..];
                    } else {
                        // force break. maybe we should error here, this shouldn't happen
                        break;
                    }
                }
            }
        }
        Ok(UdonExternIDParse {
            wrapper_name: Cow::Owned(wrapper_name),
            method_name: Cow::Owned(method_name.to_string()),
            parameters: Cow::Owned(parameters),
            return_type: Cow::Owned(remainder.to_string()),
        })
    }
}

/// In/out.
#[derive(Clone, Copy, Debug, PartialEq, Eq, PartialOrd, Ord, Serialize, Deserialize)]
pub enum UdonExternParamDir {
    In,
    Out,
    InOut,
}

/// Marks special parameters.
#[derive(Clone, Copy, Debug, PartialEq, Eq, PartialOrd, Ord, Serialize, Deserialize)]
pub enum UdonExternParamRole {
    Regular,
    This,
    Generic,
    Return,
}

#[derive(Clone, Debug, PartialEq, Eq, PartialOrd, Ord, Serialize, Deserialize)]
pub struct UdonExternParam {
    /// Udon type of the parameter as per node metadata
    pub udon_type: UdonTypeRef,
    /// Name of the parameter.
    pub name: Cow<'static, str>,
    /// 'Signature type' (from extern ID), if any.
    pub signature_type: Option<Cow<'static, str>>,
    /// Parameter role.
    pub role: UdonExternParamRole,
    /// Direction (how the heap slot is used)
    pub dir: UdonExternParamDir,
}

/// Information about an extern.
#[derive(Clone, Debug, PartialEq, Eq, PartialOrd, Ord, Serialize, Deserialize)]
pub struct UdonExtern {
    pub associated_type: UdonTypeRef,
    pub name: Cow<'static, str>,
    pub name_parsed: UdonExternIDParse,
    /// If the method is static or not.
    pub method_static: bool,
    /// Parameters.
    pub parameters: Vec<UdonExternParam>,
    /// If true, there's a generic type parameter (immediately before the return value, if any)
    pub has_generic_param: bool,
    /// If true, the last parameter is a return value.
    pub has_return: bool,
}

pub type UdonExternRef = UdonDBRef<UdonExtern>;

static UDONEXTERN_MAP: OnceLock<BTreeMap<String, UdonExtern>> = OnceLock::new();

/// Gets an [UdonType] [BTreeMap].
/// This maps the Udon type name to the corresponding [UdonType].
pub fn udonextern_map() -> &'static BTreeMap<String, UdonExtern> {
    UDONEXTERN_MAP.get_or_init(|| {
        let mut hm: BTreeMap<String, UdonExtern> = BTreeMap::new();
        for key in crate::udontype_map() {
            let typeobj = kudon_apijson::type_by_name(key.0).unwrap();
            for ext in typeobj["externs"].entries() {
                let name_parsed = UdonExternIDParse::parse(ext.0).expect("extern ID should parse");
                let has_return = !name_parsed.return_type.eq("SystemVoid");
                let parameters: Vec<UdonExternParam> = ext.1["parameters"]
                    .members()
                    .enumerate()
                    .map(|v| {
                        let name = v.1[0].as_str().expect("missing parameter name");
                        let ty = v.1[1].as_str().expect("parameter missing type");
                        let dir = v.1[2].as_str().expect("parameter missing dir");
                        UdonExternParam {
                            udon_type: udontyperef_get(ty)
                                .unwrap_or_else(|| panic!("missing type: {}", ty)),
                            name: Cow::Owned(name.to_string()),
                            signature_type: None,
                            role: UdonExternParamRole::Regular,
                            dir: if dir.eq("IN") {
                                UdonExternParamDir::In
                            } else if dir.eq("OUT") {
                                UdonExternParamDir::Out
                            } else if dir.eq("IN_OUT") {
                                UdonExternParamDir::InOut
                            } else {
                                panic!("parameter dir error: {}", dir);
                            },
                        }
                    })
                    .collect();
                // method has a generic param if it has an undocumented SystemType input that is called "type"
                let has_generic_param = !name_parsed.parameters.iter().any(|v| v.eq("SystemType"))
                    && parameters.iter().any(|v| {
                        v.udon_type.name.as_str().eq("SystemType")
                            && v.dir == UdonExternParamDir::In
                            && v.name.eq("type")
                    });
                // determine if method is static
                // we assume it's static, and then decide otherwise if it looks not static
                let mut method_static = true;
                if parameters.len() > 0
                    && parameters[0].dir == UdonExternParamDir::In
                    && parameters[0].name.eq("instance")
                    && parameters[0].udon_type.name.as_str().eq(key.0)
                {
                    method_static = false;
                }
                // emit
                let mut parsed = UdonExtern {
                    associated_type: udontyperef_get(key.0)
                        .unwrap_or_else(|| panic!("missing type: {}", key.0)),
                    name: Cow::Owned(ext.0.to_string()),
                    name_parsed,
                    method_static,
                    parameters,
                    has_generic_param,
                    has_return,
                };

                // validate methodology
                let mut metadata: Vec<(UdonExternParamRole, Option<Cow<'static, str>>)> =
                    Vec::new();
                if !method_static {
                    metadata.push((UdonExternParamRole::This, None));
                }
                for v in parsed.name_parsed.parameters.iter() {
                    metadata.push((UdonExternParamRole::Regular, Some(v.clone())));
                }
                if has_generic_param {
                    metadata.push((UdonExternParamRole::Generic, None));
                }
                if has_return {
                    metadata.push((
                        UdonExternParamRole::Return,
                        Some(parsed.name_parsed.return_type.clone()),
                    ));
                }
                assert_eq!(
                    parsed.parameters.len(),
                    metadata.len(),
                    "parameter validation failed on\n {}\n {:?}\n {:?}\n {:?}\n {:?}",
                    parsed.name,
                    parsed.parameters,
                    parsed.name_parsed.parameters,
                    parsed.name_parsed.return_type,
                    (has_generic_param, method_static, has_return)
                );

                // we can now match parameters 1:1
                for v in metadata.iter().enumerate() {
                    parsed.parameters[v.0].role = v.1.0;
                    parsed.parameters[v.0].signature_type = v.1.1.clone();
                }

                hm.insert(ext.0.to_string(), parsed);
            }
        }
        hm
    })
}

udondbentry_impl!(UdonExtern, udonextern_map);

udondbref_getters!(
    UdonExtern,
    udonextern_get,
    udonexternref_get,
    udonextern_map
);
