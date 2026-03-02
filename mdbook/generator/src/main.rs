use std::fmt::Write;
use std::collections::BTreeMap;
use kudoninfo::{UdonType, UdonTypeKind};

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
    asm.to_ascii_lowercase().replace(".", "").replace(" ", "-").replace("`", "")
}

fn unity_package_handler(what: &UdonType, pid: &str) -> String {
        let mut adjustment = what.unqualified().to_string();
        adjustment = adjustment.replace("[]", "");
        adjustment = adjustment.replace("+", ".");
        format!("https://docs.unity3d.com/Packages/{}/api/{}.html", pid, adjustment)
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
        format!("https://udonsharp.docs.vrchat.com/vrchat-api/#{}", what.short_name().to_ascii_lowercase().replace("[]", "").replace("+", "."))
    } else if asm.starts_with("Unity") {
        let mut adjustment = what.unqualified().to_string();
        adjustment = adjustment.strip_prefix("UnityEngine.").unwrap_or(adjustment.as_str()).to_string();
        adjustment = adjustment.replace("+", ".");
        adjustment = adjustment.replace("[]", "");
        format!("https://docs.unity3d.com/2022.2/Documentation/ScriptReference/{}.html", adjustment)
    } else if asm.eq("mscorlib") || asm.eq("z_Collections") {
        if what.kind == UdonTypeKind::Array {
            // wrong version, but close enough
            "https://learn.microsoft.com/en-us/dotnet/api/system.array?view=net-10.0".to_string()
        } else {
            let mut adjustment = what.unqualified().to_lowercase();
            adjustment = adjustment.replace("+", ".");
            format!("https://learn.microsoft.com/en-us/dotnet/api/{}?view=net-10.0", adjustment)
        }
    } else {
        "UNKNOWN".to_string()
    }
}

fn main() {
    // Since this is such a dangerous operation, the source directory has been renamed so it doesn't accidentally eat some other src directory.
    // We also hardcode this here so a careless change to the above constant doesn't cause this to start mass-rm'ing.
    _ = std::fs::remove_dir_all("../mdbook_generated_src");
    _ = std::fs::create_dir_all(GENSRCROOT);
    let mut summary = String::new();
    summary.push_str("# Summary\n\n");

    // -- Book body --
    let opening = include_str!("opening.md").replace("SDK_VERSION", kudoninfo::SDK_VERSION);
    put_file(&mut summary, "Opening", "opening.md", 0, &opening);

    let mut externs_index = include_str!("externs.md").to_string();

    let mut assemblies: BTreeMap<String, (String, String)> = BTreeMap::new();
    for v in kudoninfo::udontype_map() {
        let mut assembly = v.1.assembly();
        if v.0.starts_with("SystemCollectionsGeneric") {
            assembly = "z_Collections";
        }

        if !assemblies.contains_key(assembly) {
            let mut init_idx = String::new();
            if assembly.eq("z_Collections") {
                _ = writeln!(init_idx, "This is actually `mscorlib`, but this 'assembly' is being used as containment for the large quantity of generic Collection types.");
                _ = writeln!(init_idx, "");
            }
            let init_content = String::new();
            assemblies.insert(assembly.to_string(), (init_idx, init_content));
        }

        let asm = assemblies.get_mut(assembly).unwrap();

        let type_reference_fancy = format!("{:?} `{}`", v.1.kind, v.0);
        let type_reference_link = header_translate(&type_reference_fancy);

        _ = writeln!(asm.0, "* [{}](./ext_{}.md#{})", type_reference_fancy, assembly, type_reference_link);

        let udon_type = v.1;
        _ = writeln!(asm.1, "");
        _ = writeln!(asm.1, "## {}", type_reference_fancy);
        _ = writeln!(asm.1, "");
        _ = writeln!(asm.1, "[_back to assembly_](./externs.md#{})", header_translate(assembly));
        _ = writeln!(asm.1, "");
        _ = writeln!(asm.1, "* Kind: `{:?}`", udon_type.kind);
        _ = writeln!(asm.1, "* OdinSerializer: `{}`", udon_type.odin_name);
        _ = writeln!(asm.1, "* Documentation: <{}>", find_documentation_for(&udon_type));
        _ = writeln!(asm.1, "");
        _ = writeln!(asm.1, "Externs:");
        _ = writeln!(asm.1, "");
        for ext in kudoninfo::udonextern_map() {
            if !(ext.1.associated_type.as_str()).eq(v.0) {
                continue
            }
            _ = writeln!(asm.1, "* `{}`", ext.0);
        }
    }

    _ = writeln!(externs_index, "");
    _ = writeln!(externs_index, "## Assemblies");
    _ = writeln!(externs_index, "");
    for v in &assemblies {
        let asm = v.0;
        _ = writeln!(externs_index, "- [{}](./externs.md#{})", asm, header_translate(asm));
    }
    _ = writeln!(externs_index, "");

    for v in &assemblies {
        _ = writeln!(externs_index, "## {}", &v.0);
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

    for v in &assemblies {
        put_file(
            &mut summary,
            &v.0,
            &format!("ext_{}.md", v.0),
            1,
            &v.1.1,
        );
    }

    // finalize
    _ = std::fs::write(format!("{}/SUMMARY.md", GENSRCROOT), &summary);
}
