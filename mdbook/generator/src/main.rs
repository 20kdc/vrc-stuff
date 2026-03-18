use kudoninfo::{UdonExternParam, UdonExternParamDir, UdonExternParamRole, UdonType, UdonTypeKind};
use std::collections::BTreeMap;
use std::fmt::Write;

static GENSRCROOT: &'static str = "../mdbook_generated_src";

fn put_toc(summary: &mut String, title: &str, id: &str, level: usize) {
    for _ in 0..level {
        _ = write!(summary, "\t");
    }
    _ = writeln!(summary, "- [{}](./{})", title, id);
}

fn put_file(summary: &mut String, title: &str, id: &str, level: usize, text: &str) {
    _ = std::fs::write(format!("{}/{}", GENSRCROOT, id), text);
    put_toc(summary, title, id, level);
}

fn header_translate(asm: &str) -> String {
    asm.to_ascii_lowercase()
        .replace(".", "")
        .replace(" ", "-")
        .replace("`", "")
}

fn unity_package_handler(what: &UdonType, pid: &str) -> String {
    let mut adjustment = what.unqualified().to_string();
    adjustment = adjustment.replace("[]", "");
    adjustment = adjustment.replace("+", ".");
    format!(
        "https://docs.unity3d.com/Packages/{}/api/{}.html",
        pid, adjustment
    )
}

fn find_documentation_for(what: &UdonType) -> String {
    let asm = what.assembly();
    if asm.eq("Cinemachine") {
        unity_package_handler(what, "com.unity.cinemachine@2.3")
    } else if asm.eq("Unity.AI.Navigation") {
        unity_package_handler(what, "com.unity.ai.navigation@1.1")
    } else if asm.eq("Unity.TextMeshPro") {
        unity_package_handler(what, "com.unity.ugui@2.0")
    } else if asm.eq("Unity.Postprocessing.Runtime") {
        unity_package_handler(what, "com.unity.postprocessing@2.0")
    } else if asm.eq("VRCEconomy") || what.name.as_str().eq("VRCSDK3ComponentsVRCOpenMenu") {
        "https://creators.vrchat.com/economy/sdk/udon-documentation".to_string()
    } else if asm.starts_with("VRC") {
        let mut name = what.short_name().to_ascii_lowercase().replace("[]", "");
        if let Some(ptr) = name.find("+") {
            name = name[ptr + 1..].to_string();
        }
        format!("https://udonsharp.docs.vrchat.com/vrchat-api/#{}", name)
    } else if asm.starts_with("Unity") {
        let mut adjustment = what.unqualified().to_string();
        adjustment = adjustment
            .strip_prefix("UnityEngine.")
            .unwrap_or(adjustment.as_str())
            .to_string();
        adjustment = adjustment.replace("+", ".");
        adjustment = adjustment.replace("[]", "");
        format!(
            "https://docs.unity3d.com/2022.3/Documentation/ScriptReference/{}.html",
            adjustment
        )
    } else if asm.eq("mscorlib") || asm.eq("Collections") || asm.eq("System") {
        if what.kind == UdonTypeKind::Array {
            // wrong version, but close enough
            "https://learn.microsoft.com/en-us/dotnet/api/system.array?view=net-10.0".to_string()
        } else {
            let mut adjustment = what.unqualified().to_lowercase();
            adjustment = adjustment.replace("+", ".");
            format!(
                "https://learn.microsoft.com/en-us/dotnet/api/{}?view=net-10.0",
                adjustment
            )
        }
    } else {
        "UNKNOWN".to_string()
    }
}

#[derive(Clone, Copy, PartialOrd, Ord, PartialEq, Eq)]
enum PageCat {
    Regular,
    Collections,
    Arrays,
}

type PageRef = (PageCat, String);

struct TypeInfo {
    /// Page reference.
    page: PageRef,
    /// Link to the type.
    link: String,
    /// Fancy reference (used for header)
    fancy: String,
    /// The underlying UdonType.
    udon_type: &'static UdonType,
}

fn gen_typeinfo() -> BTreeMap<String, TypeInfo> {
    let mut typemap: BTreeMap<String, TypeInfo> = BTreeMap::new();
    for v in kudoninfo::udontype_map() {
        let mut assembly = (PageCat::Regular, v.1.assembly().to_string());
        if v.0.starts_with("SystemCollectionsGeneric") {
            assembly = (PageCat::Collections, "Collections".to_string());
        }
        if v.1.kind == UdonTypeKind::Array {
            assembly = (PageCat::Arrays, format!("{}_arrays", assembly.1));
        }

        let type_reference_fancy = format!("{:?} `{}`", v.1.kind, v.0);
        let type_reference_link = format!(
            "ext/{}.md#{}",
            assembly.1,
            header_translate(&type_reference_fancy)
        );

        typemap.insert(
            v.0.to_string(),
            TypeInfo {
                page: assembly,
                link: type_reference_link,
                fancy: type_reference_fancy,
                udon_type: v.1,
            },
        );
    }
    typemap
}

fn translate_parameter_type(
    tree: &BTreeMap<String, TypeInfo>,
    st: &UdonExternParam,
    link_pfx: &str,
) -> String {
    let mut res = if let Some(x) = &st.signature_type {
        x
    } else {
        st.udon_type.name.as_str()
    };
    res = res.strip_suffix("Ref").unwrap_or(res);
    if let Some(v) = tree.get(st.udon_type.name.as_str()) {
        format!("[{}]({}{})", res, link_pfx, v.link)
    } else {
        format!("{}", res)
    }
}

fn main() {
    // Since this is such a dangerous operation, the source directory has been renamed so it doesn't accidentally eat some other src directory.
    // We also hardcode this here so a careless change to the above constant doesn't cause this to start mass-rm'ing.
    _ = std::fs::remove_dir_all("../mdbook_generated_src");
    _ = std::fs::create_dir_all(GENSRCROOT);
    _ = std::fs::create_dir_all(format!("{}/ext", GENSRCROOT));
    let mut summary = String::new();
    summary.push_str("# Summary\n\n");

    // -- Book body --
    let opening = include_str!("opening.md").replace("SDK_VERSION", kudoninfo::SDK_VERSION);
    put_file(&mut summary, "Opening", "opening.md", 0, &opening);

    put_file(
        &mut summary,
        "vrc-stuff Unity Packages And Their Contents",
        "kvtools.md",
        0,
        include_str!("kvtools.md"),
    );

    put_file(
        &mut summary,
        "'KDCBSP': Quake 2 BSP import for world design",
        "bsp.md",
        1,
        include_str!("../../../kvbsp/README.md"),
    );

    put_file(
        &mut summary,
        "Version Control Practices",
        "vcs.md",
        1,
        include_str!("vcs.md"),
    );

    put_file(
        &mut summary,
        "Udon VM (Short Primer)",
        "udon_vm_primer.md",
        0,
        include_str!("udon_vm_primer.md"),
    );

    put_file(
        &mut summary,
        "OdinSerializer",
        "odinserializer.md",
        1,
        include_str!("odinserializer.md"),
    );

    put_file(
        &mut summary,
        "Udon Program Format",
        "udon_container.md",
        1,
        include_str!("udon_container.md"),
    );

    put_file(
        &mut summary,
        "Name Mangling",
        "udon_mangling.md",
        1,
        include_str!("udon_mangling.md"),
    );

    let mut externs_index = include_str!("externs.md").to_string();

    // Map types to pages and add assemblies.
    let typeinfo: BTreeMap<String, TypeInfo> = gen_typeinfo();
    let mut typepages: BTreeMap<PageRef, (String, String)> = BTreeMap::new();

    for v in &typeinfo {
        let ti = &v.1;

        if !typepages.contains_key(&ti.page) {
            let mut init_idx = String::new();
            if ti.page.0 == PageCat::Collections {
                _ = writeln!(
                    init_idx,
                    "This is actually `mscorlib`, but this 'assembly' is being used as containment for the large quantity of generic Collection types."
                );
                _ = writeln!(init_idx, "");
            }
            if ti.page.0 == PageCat::Arrays {
                _ = writeln!(
                    init_idx,
                    "This is not actually a separate assembly, but array types tend to clutter up the main listings."
                );
                _ = writeln!(init_idx, "");
            }
            let init_content = String::new();
            typepages.insert(ti.page.clone(), (init_idx, init_content));
        }

        let asm = typepages.get_mut(&ti.page).unwrap();

        // The short name makes the search easier to use.
        _ = writeln!(
            asm.0,
            "* [{}]({}) (`{}`)",
            ti.fancy,
            ti.link,
            ti.udon_type.short_name()
        );

        let udon_type = ti.udon_type;
        _ = writeln!(asm.1, "");
        _ = writeln!(asm.1, "## {}", ti.fancy);
        _ = writeln!(asm.1, "");
        _ = writeln!(
            asm.1,
            "[_back to assembly_](../externs.md#{})",
            header_translate(&ti.page.1)
        );
        _ = writeln!(asm.1, "");
        if let Some(other_name) = udon_type.name.strip_suffix("Array") {
            if let Some(other_type) = typeinfo.get(other_name) {
                _ = writeln!(asm.1, "[_back to element type_](../{})", other_type.link);
                _ = writeln!(asm.1, "");
            }
        }
        if let Some(other_type) = typeinfo.get(&format!("{}Array", udon_type.name)) {
            _ = writeln!(asm.1, "[_to array type_](../{})", other_type.link);
            _ = writeln!(asm.1, "");
        }
        _ = writeln!(asm.1, "* Kind: `{:?}`", udon_type.kind);
        _ = writeln!(asm.1, "* OdinSerializer: `{}`", udon_type.odin_name);
        _ = writeln!(
            asm.1,
            "* Documentation: <{}>",
            find_documentation_for(&udon_type)
        );
        _ = writeln!(asm.1, "");
        if let Some(enum_values) = &udon_type.enum_values {
            _ = writeln!(asm.1, "Enum values:");
            _ = writeln!(asm.1, "");
            for v in enum_values {
                _ = writeln!(asm.1, "* `{} = {}`", v.0, v.1);
            }
            _ = writeln!(asm.1, "");
        }
        for ext in kudoninfo::udonextern_map() {
            let ext = ext.1;
            if !(ext.associated_type.name.as_str()).eq(v.0) {
                continue;
            }
            _ = writeln!(asm.1, "### `{}`", ext.name);
            _ = writeln!(asm.1, "");
            // we now attempt to format this extern in a sensible way
            _ = write!(asm.1, "<code>");
            if ext.method_static {
                _ = write!(asm.1, "static ");
            }
            if ext.has_return {
                _ = write!(
                    asm.1,
                    "{} ",
                    translate_parameter_type(&typeinfo, ext.parameters.last().unwrap(), "../")
                );
            } else {
                _ = write!(asm.1, "void ");
            }
            if ext.has_generic_param {
                _ = write!(asm.1, "{}&lt;T&gt;(", ext.name_parsed.method_name);
            } else {
                _ = write!(asm.1, "{}(", ext.name_parsed.method_name);
            }
            let mut was_first = true;
            for param in ext.parameters.iter() {
                if param.role == UdonExternParamRole::Regular {
                    if !was_first {
                        _ = write!(asm.1, ", ");
                    } else {
                        was_first = false;
                    }
                    match param.dir {
                        UdonExternParamDir::In => {
                            // normal
                        }
                        UdonExternParamDir::Out => {
                            _ = write!(asm.1, "out ");
                        }
                        UdonExternParamDir::InOut => {
                            _ = write!(asm.1, "ref ");
                        }
                    }
                    _ = write!(
                        asm.1,
                        "{}",
                        translate_parameter_type(&typeinfo, param, "../")
                    );
                }
            }
            _ = writeln!(asm.1, ");</code>");
            _ = writeln!(asm.1, "");
            _ = write!(asm.1, "Raw: `");
            was_first = true;
            for param in ext.parameters.iter() {
                if !was_first {
                    _ = write!(asm.1, ", ");
                } else {
                    was_first = false;
                }
                _ = write!(asm.1, "{:?}({})", param.dir, param.udon_type.name);
            }
            _ = writeln!(asm.1, "`");
        }
    }

    _ = writeln!(externs_index, "");
    _ = writeln!(externs_index, "## Assemblies");
    _ = writeln!(externs_index, "");
    for v in &typepages {
        let asm = v.0;
        _ = writeln!(
            externs_index,
            "- [{}](./externs.md#{})",
            asm.1,
            header_translate(&asm.1)
        );
    }
    _ = writeln!(externs_index, "");

    for v in &typepages {
        _ = writeln!(externs_index, "## {}", &v.0.1);
        _ = writeln!(externs_index, "");
        _ = writeln!(externs_index, "[_back to top_](./externs.md#assemblies)");
        _ = writeln!(externs_index, "");
        _ = writeln!(externs_index, "{}", &v.1.0);
    }

    put_file(
        &mut summary,
        "Extern Reference",
        "externs.md",
        0,
        &externs_index,
    );

    // -- clutter; MUST BE LAST --

    put_file(
        &mut summary,
        "Externs Body",
        "externs_body.md",
        0,
        &"The externs documentation itself goes here, so it doesn't get in the way.",
    );

    for v in &typepages {
        put_file(
            &mut summary,
            &v.0.1,
            &format!("ext/{}.md", v.0.1),
            1,
            &v.1.1,
        );
    }

    // finalize
    _ = std::fs::write(format!("{}/SUMMARY.md", GENSRCROOT), &summary);
}
