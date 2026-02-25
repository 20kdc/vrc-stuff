//! The librarian module implements libraries.

use crate::*;
use kudonast::UdonProgram;

use std::collections::HashMap;

pub struct KU2Package {
    pub name: String,
    pub deps: Vec<String>,
    pub contents: Vec<(String, KU2Instruction)>,
}

pub type KU2Librarian = HashMap<String, KU2Package>;

impl KU2Package {
    /// Assembles the body of the package.
    pub fn assemble(&self, asm: &mut KU2Context, file: &mut UdonProgram) -> Result<(), String> {
        for line in &self.contents {
            asm.assemble(file, &line.1)
                .map_err(|err| format!("@{}: {}", line.0, err))?;
        }
        Ok(())
    }

    /// Assembles a package by name. Installs dependencies automatically.
    pub fn assemble_by_name(
        asm: &mut KU2Context,
        file: &mut UdonProgram,
        librarian: &KU2Librarian,
        package: &str,
    ) -> Result<(), String> {
        if let Some(pkg) = librarian.get(package) {
            pkg.install_deps(asm, file, librarian)?;
            pkg.assemble(asm, file)?;
            Ok(())
        } else {
            Err(format!("Wanted package {}, not found", package))
        }
    }

    /// Installs dependencies.
    pub fn install_deps(
        &self,
        asm: &mut KU2Context,
        file: &mut UdonProgram,
        librarian: &KU2Librarian,
    ) -> Result<(), String> {
        for v in &self.deps {
            if !asm.packages.contains(v) {
                asm.packages.insert(v.clone());
                Self::assemble_by_name(asm, file, librarian, v)?;
            }
        }
        Ok(())
    }

    /// Splits an instruction list into packages and leftovers, while cleaning up the source line indicator.
    pub fn split(
        dst: &mut KU2Librarian,
        src_file: &str,
        mut src: Vec<(usize, KU2Instruction)>,
    ) -> Vec<(String, KU2Instruction)> {
        let mut leftovers = Vec::new();
        let mut current_package: Option<KU2Package> = None;

        for instruction in src.drain(..) {
            if let KU2Instruction::Package(name, deps) = instruction.1 {
                if let Some(package) = current_package.take() {
                    dst.insert(package.name.clone(), package);
                }
                if let Some(mut package) = dst.remove(&name) {
                    // For appending, we 'check out' a package by removing it.
                    // This is much simpler than the alternatives.
                    for v in deps {
                        if !package.deps.contains(&v) {
                            package.deps.push(v);
                        }
                    }
                    current_package = Some(package);
                } else {
                    current_package = Some(KU2Package {
                        name,
                        deps,
                        contents: Vec::new(),
                    });
                }
            } else {
                let reformatted = (
                    format!(
                        "{}:{}:{}",
                        src_file,
                        if let Some(package) = &current_package {
                            package.name.as_str()
                        } else {
                            "none"
                        },
                        instruction.0
                    ),
                    instruction.1,
                );
                if let Some(cpb) = &mut current_package {
                    cpb.contents.push(reformatted);
                } else {
                    leftovers.push(reformatted);
                }
            }
        }

        if let Some(package) = current_package.take() {
            dst.insert(package.name.clone(), package);
        }

        leftovers
    }

    /// Adds a blank package if it does not already exist.
    pub fn add_blank(dst: &mut KU2Librarian, name: &str) {
        if !dst.contains_key(name) {
            dst.insert(
                name.to_string(),
                KU2Package {
                    name: name.to_string(),
                    deps: Vec::new(),
                    contents: Vec::new(),
                },
            );
        }
    }
}
