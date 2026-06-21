use crate::{
    UdonExtern, UdonExternParam, UdonExternParamDir, UdonExternParamRole, udonextern_map,
    udontype_map,
};
use std::collections::BTreeMap;
use std::sync::OnceLock;

static UDONEXTERNLOOKUP_MAP: OnceLock<
    BTreeMap<String, BTreeMap<String, Vec<&'static UdonExtern>>>,
> = OnceLock::new();

/// Method naming rules used by udonexternlookup.
pub fn udonexternlookup_adjmethodname(ue: &UdonExtern) -> String {
    let mut method_name = ue.name_parsed.method_name.to_string();
    if ue.has_generic_param {
        method_name.push_str("<T>");
    }
    method_name
}

/// Type casting rules used by udonexternlookup.
/// These must be valid type names.
/// This will never panic (if the type is unknown, it will simply have zero bases).
pub fn udonexternlookup_typecast(supertype: &str, subtype: &str) -> bool {
    if supertype.eq(subtype) {
        return true;
    }
    if let Some(bases) = kudon_apijson::type_bases(subtype, false) {
        for v in bases {
            if v.eq(supertype) {
                return true;
            }
        }
    }
    false
}

/// This determines if the parameters of superext include those of subext.
/// This isn't a precise emulation of C# lookup rules; still, it aims to Do The Right Thing, Most Of The Time.
pub fn udonexternlookup_parameters_include(
    superext: &[UdonExternParam],
    subext: &[UdonExternParam],
) -> bool {
    if superext.len() != subext.len() {
        return false;
    }
    for i in 0..subext.len() {
        let superpar = &superext[i];
        let subpar = &subext[i];
        if superpar.role != subpar.role {
            return false;
        }
        if superpar.dir != subpar.dir {
            return false;
        }
        if superpar.dir == UdonExternParamDir::In {
            // input
            // subpar can be a base of superpar, but not vice versa
            if !udonexternlookup_typecast(&superpar.udon_type.name, &subpar.udon_type.name) {
                return false;
            }
        } else {
            // output/other
            // outputs can be more precisely specified; superpar can be a base of subpar
            if !udonexternlookup_typecast(&subpar.udon_type.name, &superpar.udon_type.name) {
                return false;
            }
        }
    }
    true
}

/// Inserts an extern of same method name and generic arity into a list.
/// Removes existing entries, etc. based on shadowing/override rules.
fn udonexternlookup_insert_with_shadowing(
    existing: &mut Vec<&'static UdonExtern>,
    ext: &'static UdonExtern,
) {
    let mut ptr: usize = 0;
    while ptr < existing.len() {
        let ex = &existing[ptr];
        if ex.method_static == ext.method_static
            && udonexternlookup_parameters_include(&ex.parameters, &ext.parameters)
        {
            existing.swap_remove(ptr);
        } else {
            ptr += 1
        }
    }
    existing.push(ext);
}

/// This map maps [UdonType]s to relevant externs.
/// These are split by method name. Generic arity is encoded as `<T>`.
/// Note that this should NOT be the only method you have of invoking externs.
/// It's very likely there are edge cases which get caught up in the gears.
pub fn udonexternlookup_map()
-> &'static BTreeMap<String, BTreeMap<String, Vec<&'static UdonExtern>>> {
    UDONEXTERNLOOKUP_MAP.get_or_init(|| {
        // Map of all directly attached externs.
        let mut direct_map: BTreeMap<String, Vec<&'static UdonExtern>> = BTreeMap::new();
        // create for all types
        for utm in udontype_map().keys() {
            direct_map.insert(utm.clone(), Vec::new());
        }
        // create for all externs
        for ext in udonextern_map() {
            let target_list = direct_map
                .get_mut(ext.1.associated_type.name.as_str())
                .expect("extern associated types should correspond to valid udontypes");
            target_list.push(ext.1);
        }
        let mut map = BTreeMap::new();
        // We build a map per-UdonType, because we need to account for inheritance/etc.
        for udontype in udontype_map().values() {
            let mut methodmap = BTreeMap::new();
            let lookup_order =
                kudon_apijson::type_bases(&udontype.name, true).expect("type bases should resolve");
            for erz in lookup_order {
                // Insert each relevant extern, one by one.
                for ext in direct_map
                    .get(erz)
                    .expect("direct_map should be fully populated")
                {
                    let adj = udonexternlookup_adjmethodname(ext);
                    // prepare
                    // insert
                    if let Some(existing) = methodmap.get_mut(&adj) {
                        udonexternlookup_insert_with_shadowing(existing, ext);
                    } else {
                        let mut sign: Vec<&'static UdonExtern> = Vec::new();
                        sign.push(ext);
                        methodmap.insert(adj, sign);
                    }
                }
            }
            map.insert(udontype.name.to_string(), methodmap);
        }
        map
    })
}

/// Searches for Udon instance method externs that match the given requirements.
/// (This is for use in the 'exi' KU2 macroinstruction, but is generally applicable.)
pub fn udonexternlookup_exi(
    this: &str,
    method_name: &str,
    params: &[String],
) -> Vec<&'static UdonExtern> {
    let mut vec = Vec::new();
    if let Some(map) = udonexternlookup_map().get(this) {
        if let Some(methods) = map.get(method_name) {
            // alright, 'methods' is our candidate list. these methods must:
            // 1. NOT be instance methods
            // 2. MUST have 'this' as first parameter (this might be changed, but if so KU2 codegen must be updated accordingly)
            // 3. MUST have the correct number of parameters
            // 4. 'in' parameters MUST be castable appropriately
            for candidate in methods {
                if candidate.method_static {
                    continue;
                }
                if candidate.parameters.len() != (params.len() + 1) {
                    continue;
                }
                if candidate.parameters[0].role != UdonExternParamRole::This {
                    continue;
                }
                let mut ok = true;
                for (param_idx, param) in candidate.parameters.iter().enumerate() {
                    let comparison = if param_idx == 0 {
                        this
                    } else {
                        &params[param_idx - 1]
                    };
                    if param.dir == UdonExternParamDir::In {
                        if !udonexternlookup_typecast(comparison, &param.udon_type.name) {
                            ok = false;
                            break;
                        }
                    }
                }
                if ok {
                    vec.push(*candidate);
                }
            }
        }
    }
    vec
}

/// Searches for Udon static method externs that match the given requirements.
/// This is for use in the 'exop' KU2 macroinstruction, and is a little precise about what it wants.
/// In particular, every parameter is checked as a potentially relevant type.
/// This is designed around supporting `op_` functions.
pub fn udonexternlookup_exop(method_name: &str, params: &[String]) -> Vec<&'static UdonExtern> {
    let mut vec: Vec<&'static UdonExtern> = Vec::new();
    for typefind_param in params.iter().filter_map(|v| udonexternlookup_map().get(v)) {
        if let Some(methods) = typefind_param.get(method_name) {
            for candidate in methods {
                if !candidate.method_static {
                    continue;
                }
                if candidate.parameters.len() != params.len() {
                    continue;
                }
                let mut ok = true;
                // deduplicate
                for other in &vec {
                    if other.name.eq(&candidate.name) {
                        ok = false;
                        break;
                    }
                }
                if !ok {
                    continue;
                }
                for (param_idx, param) in candidate.parameters.iter().enumerate() {
                    let comparison = &params[param_idx];
                    if param.dir == UdonExternParamDir::In {
                        if !udonexternlookup_typecast(comparison, &param.udon_type.name) {
                            ok = false;
                            break;
                        }
                    } else {
                        if !udonexternlookup_typecast(&param.udon_type.name, comparison) {
                            ok = false;
                            break;
                        }
                    }
                }
                if ok {
                    vec.push(*candidate);
                }
            }
        }
    }
    vec
}
