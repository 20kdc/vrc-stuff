//! `kudonasm` is a replacement assembly language for Udon.

use kudonast::{UdonAccess, UdonHeapSlot, UdonHeapValue, UdonInt, UdonProgram, UdonSymbol};
use kudoninfo::{UdonOpcode, UdonTypeRef};
use kudonodin::{OdinIntType, OdinPrimitive};
use std::collections::{BTreeMap, BTreeSet};

mod parsing;
pub use parsing::*;

/// Represents a loaded package/snippet.
#[derive(Clone, Debug)]
pub struct KU2Package {
    pub name: String,
    pub deps: BTreeSet<String>,
    pub contents: Vec<(String, KU2Instruction)>,
}

#[derive(Clone, Debug)]
pub struct KU2Context {
    pub equates: BTreeMap<String, UdonInt>,
    /// Packages that have actually been installed.
    pub installed_packages: BTreeSet<String>,
    /// Packages.
    /// Note that packages are temporarily removed while being executed or edited.
    /// It's an ugly hack, but it works.
    pub packages: BTreeMap<String, KU2Package>,
    /// The package being edited.
    /// While being edited, the package is not available for invoke.
    pub editing_package: Option<KU2Package>,
}

impl Default for KU2Context {
    fn default() -> Self {
        let mut equates = BTreeMap::new();
        equates.insert("_".to_string(), UdonInt::I(0));
        Self {
            equates,
            installed_packages: Default::default(),
            packages: Default::default(),
            editing_package: None,
        }
    }
}

impl KU2Context {
    pub fn ku2sym_to_udon(&self, v: &KU2Symbol) -> Result<String, String> {
        if let Some(equ) = self.equates.get(&v.0) {
            if let UdonInt::Sym(sym) = equ {
                Ok(sym.clone())
            } else {
                Err(format!(
                    "Symbol {} is already an equate which isn't a symbol",
                    &v.0
                ))
            }
        } else {
            Ok(v.0.clone())
        }
    }
    /// Parses an operand to [UdonInt].
    pub fn operand_udonint(
        &mut self,
        file: &mut UdonProgram,
        v: &KU2Operand,
        affinity: KU2StringAffinity,
    ) -> Result<UdonInt, String> {
        match v {
            KU2Operand::Sym(sym, mods) => {
                let mut base = if let Some(equ) = self.equates.get(&sym.0) {
                    // Existing equate
                    equ.clone()
                } else {
                    UdonInt::Sym(sym.0.clone())
                };
                for v in mods {
                    base = match v {
                        KU2Modifier::Add(op) => UdonInt::Add(
                            Box::new(base),
                            Box::new(self.operand_udonint(file, op, affinity)?),
                        ),
                        KU2Modifier::Sub(op) => UdonInt::Sub(
                            Box::new(base),
                            Box::new(self.operand_udonint(file, op, affinity)?),
                        ),
                        KU2Modifier::Mul(op) => UdonInt::Mul(
                            Box::new(base),
                            Box::new(self.operand_udonint(file, op, affinity)?),
                        ),
                        KU2Modifier::HeapConst(ic) => {
                            let v = base
                                .resolve(&file.internal_syms)
                                .map_err(|v| format!("in heap_const: {}", v))?;
                            UdonInt::Sym(file.ensure_iconst(*ic, v))
                        }
                    };
                }
                Ok(base)
            }
            KU2Operand::Str(v) => match affinity {
                KU2StringAffinity::Data => Ok(UdonInt::Sym(file.ensure_string(&v, false))),
                KU2StringAffinity::Extern => Ok(UdonInt::Sym(file.ensure_string(&v, true))),
                KU2StringAffinity::Char => {
                    let chars: Vec<char> = v.chars().collect();
                    if chars.len() != 1 {
                        Err(format!(
                            "'{}' interpreted as character constant, but wasn't a single codepoint",
                            &v
                        ))
                    } else {
                        Ok(UdonInt::I(chars[0] as i64))
                    }
                }
                KU2StringAffinity::Error => Err(format!(
                    "String '{}' used, but unsure how to interpret. Consider 'equ_str'.",
                    &v
                )),
            },
            KU2Operand::I(v) => Ok(UdonInt::I(*v)),
        }
    }
    pub fn assemble_op(
        &mut self,
        file: &mut UdonProgram,
        opcode: &'static UdonOpcode,
        stuff: &[&KU2Operand],
    ) -> Result<(), String> {
        file.code.push(kudonast::UdonInt::Op(opcode));
        for v in stuff.iter().enumerate() {
            let affinity = match opcode.parameters[v.0] {
                kudoninfo::UdonSpaciality::Indistinct => KU2StringAffinity::Data,
                kudoninfo::UdonSpaciality::Annotation => KU2StringAffinity::Data,
                kudoninfo::UdonSpaciality::Data => KU2StringAffinity::Data,
                kudoninfo::UdonSpaciality::DataExtern => KU2StringAffinity::Extern,
                kudoninfo::UdonSpaciality::Code => KU2StringAffinity::Error,
            };
            let res = self.operand_udonint(file, v.1, affinity)?;
            file.code.push(res);
        }
        Ok(())
    }
    pub fn create_label(
        &self,
        internal_syms: &mut BTreeMap<String, i64>,
        symtab: &mut Vec<UdonSymbol>,
        sym: &KU2Symbol,
        ty: Option<UdonTypeRef>,
        acc: Option<UdonAccess>,
        loc: i64,
    ) -> Result<(), String> {
        let sym_remap = self.ku2sym_to_udon(sym)?;
        if internal_syms.contains_key(&sym_remap) {
            Err(format!(
                "internal symbol {} ({}) declared twice; to overlap code/data syms in the output, consider using rename_sym",
                sym_remap, sym.0
            ))
        } else {
            internal_syms.insert(sym_remap.clone(), loc);
            if let Some(acc) = acc {
                symtab.push(UdonSymbol {
                    name: sym_remap.clone(),
                    udon_type: ty.clone(),
                    address: UdonInt::Sym(sym_remap.clone()),
                    mode: acc,
                });
            }
            Ok(())
        }
    }

    pub fn conv_heap_slot(
        &mut self,
        file: &mut UdonProgram,
        val: &KU2HeapSlot,
    ) -> Result<UdonHeapSlot, String> {
        macro_rules! pmap {
            ($udon_type:ident, $variant:ident, $v:expr) => {
                Ok(UdonHeapSlot(
                    (&kudoninfo::udon_types::$udon_type).into(),
                    UdonHeapValue::P(OdinPrimitive::$variant($v.clone())),
                ))
            };
        }
        macro_rules! imap {
            ($variant:ident, $v:expr, $a:ident) => {
                Ok(UdonHeapSlot(
                    kudonast::odininttype_to_udontype(OdinIntType::$variant),
                    UdonHeapValue::I(
                        OdinIntType::$variant,
                        self.operand_udonint(file, $v, KU2StringAffinity::$a)?,
                    ),
                ))
            };
        }
        match val {
            KU2HeapSlot::String(v) => pmap!(SystemString, String, v),

            KU2HeapSlot::SByte(v) => imap!(SByte, v, Error),
            KU2HeapSlot::Byte(v) => imap!(Byte, v, Error),
            KU2HeapSlot::Short(v) => imap!(Short, v, Error),
            KU2HeapSlot::UShort(v) => imap!(UShort, v, Error),
            KU2HeapSlot::Int(v) => imap!(Int, v, Error),
            KU2HeapSlot::UInt(v) => imap!(UInt, v, Error),
            KU2HeapSlot::Long(v) => imap!(Long, v, Error),
            KU2HeapSlot::ULong(v) => imap!(ULong, v, Error),
            KU2HeapSlot::Float(v) => pmap!(SystemSingle, Float, v),
            KU2HeapSlot::Double(v) => pmap!(SystemDouble, Double, v),
            KU2HeapSlot::Char(v) => imap!(Char, v, Char),

            KU2HeapSlot::True => pmap!(SystemBoolean, Boolean, true),
            KU2HeapSlot::False => pmap!(SystemBoolean, Boolean, false),

            KU2HeapSlot::Null(ot) => Ok(UdonHeapSlot(
                ot.clone(),
                UdonHeapValue::P(OdinPrimitive::Null),
            )),

            KU2HeapSlot::This(ot) => Ok(UdonHeapSlot(ot.clone(), UdonHeapValue::This)),
            KU2HeapSlot::Custom(ot, ov) => Ok(UdonHeapSlot(ot.clone(), ov.clone())),
        }
    }

    pub fn assemble_var(
        &mut self,
        file: &mut UdonProgram,
        sym: &KU2Symbol,
        val: &KU2HeapSlot,
        acc: Option<UdonAccess>,
    ) -> Result<(), String> {
        let loc = file.data.len();
        let uhs: UdonHeapSlot = self.conv_heap_slot(file, val)?;
        file.data.push(uhs.clone());
        self.create_label(
            &mut file.internal_syms,
            &mut file.data_syms,
            sym,
            Some(uhs.0),
            acc,
            loc as i64,
        )
    }

    /// Assembles a single instruction of code.
    /// Notably, this function doesn't add the file/line string to the errors.
    pub fn assemble_no_line_info(
        &mut self,
        file: &mut UdonProgram,
        loc: &str,
        instr: &KU2Instruction,
    ) -> Result<(), String> {
        let code_base_ptr = (file.code.len() * 4) as i64;

        if let KU2Instruction::Package(name, deps) = instr {
            self.end_package();
            let mut pkg = if let Some(pkg) = self.packages.remove(name) {
                pkg
            } else {
                KU2Package {
                    name: name.clone(),
                    deps: BTreeSet::new(),
                    contents: vec![],
                }
            };
            for v in deps {
                pkg.deps.insert(v.clone());
            }
            self.editing_package = Some(pkg);
            return Ok(());
        } else if let KU2Instruction::PackageEnd = instr {
            self.end_package();
            return Ok(());
        } else if let Some(editing_package) = &mut self.editing_package {
            // Editing a package, so append.
            editing_package
                .contents
                .push((loc.to_string(), instr.clone()));
            return Ok(());
        }

        match instr {
            // -- decl --
            KU2Instruction::VarInternal(sym, val) => self.assemble_var(file, sym, val, None),
            KU2Instruction::VarElidable(sym, val) => {
                self.assemble_var(file, sym, val, Some(UdonAccess::Elidable))
            }
            KU2Instruction::VarSymbol(sym, val) => {
                self.assemble_var(file, sym, val, Some(UdonAccess::Symbol))
            }
            KU2Instruction::VarPublic(sym, val) => {
                self.assemble_var(file, sym, val, Some(UdonAccess::Public))
            }
            KU2Instruction::Sync(sym, synctype) => {
                let sym = self.ku2sym_to_udon(&sym)?;
                let synctype: u64 = (*synctype).into();
                file.sync_metadata.push((sym, "this".to_string(), synctype));
                Ok(())
            }
            KU2Instruction::SyncProp(sym, prop, synctype) => {
                let sym = self.ku2sym_to_udon(&sym)?;
                let synctype: u64 = (*synctype).into();
                file.sync_metadata.push((sym, prop.clone(), synctype));
                Ok(())
            }
            KU2Instruction::UpdateOrder(operand) => {
                file.update_order =
                    self.operand_udonint(file, operand, KU2StringAffinity::Error)?;
                Ok(())
            }
            KU2Instruction::NetEvent(subr, maxeps, params) => {
                let mut parameters = vec![];
                for v in params {
                    parameters.push((self.ku2sym_to_udon(&v.0)?, v.1.clone()));
                }
                file.network_call_metadata
                    .push(kudonast::UdonNetworkCallMetadata {
                        name: self.ku2sym_to_udon(&subr)?,
                        max_events_per_second: *maxeps,
                        parameters,
                    });
                Ok(())
            }
            KU2Instruction::RenameSym(from, to) => {
                for v in &mut file.code_syms {
                    if v.name.eq(&from.0) {
                        v.name.clone_from(to);
                    }
                }
                for v in &mut file.data_syms {
                    if v.name.eq(&from.0) {
                        v.name.clone_from(to);
                    }
                }
                for v in &mut file.sync_metadata {
                    if v.0.eq(&from.0) {
                        v.0.clone_from(to);
                    }
                }
                for v in &mut file.network_call_metadata {
                    if v.name.eq(&from.0) {
                        v.name.clone_from(to);
                    }
                    for param in &mut v.parameters {
                        if param.0.eq(&from.0) {
                            param.0.clone_from(to);
                        }
                    }
                }
                Ok(())
            }
            // -- meta --
            KU2Instruction::Package(_, _) => {
                unreachable!();
            }
            KU2Instruction::PackageEnd => {
                unreachable!();
            }
            KU2Instruction::CodeComment(comm) => {
                UdonProgram::add_comment(&mut file.code_comments, file.code.len(), comm);
                Ok(())
            }
            KU2Instruction::DataComment(comm) => {
                UdonProgram::add_comment(&mut file.data_comments, file.data.len(), comm);
                Ok(())
            }
            // -- codelabel --
            KU2Instruction::CodeInternal(sym) => self.create_label(
                &mut file.internal_syms,
                &mut file.code_syms,
                sym,
                None,
                None,
                code_base_ptr,
            ),
            KU2Instruction::CodeSymbol(sym) => self.create_label(
                &mut file.internal_syms,
                &mut file.code_syms,
                sym,
                None,
                Some(UdonAccess::Symbol),
                code_base_ptr,
            ),
            KU2Instruction::CodeElidable(sym) => self.create_label(
                &mut file.internal_syms,
                &mut file.code_syms,
                sym,
                None,
                Some(UdonAccess::Elidable),
                code_base_ptr,
            ),
            KU2Instruction::CodePublic(sym) => self.create_label(
                &mut file.internal_syms,
                &mut file.code_syms,
                sym,
                None,
                Some(UdonAccess::Public),
                code_base_ptr,
            ),
            // -- equate --
            KU2Instruction::EquateInt(sym, operand) => {
                let value = self.operand_udonint(file, operand, KU2StringAffinity::Error)?;
                self.equates.insert(sym.0.clone(), value);
                Ok(())
            }
            KU2Instruction::EquateStr(sym, affinity, operand) => {
                let value = self.operand_udonint(file, operand, *affinity)?;
                self.equates.insert(sym.0.clone(), value);
                Ok(())
            }
            KU2Instruction::Local(sym) => {
                self.equates
                    .insert(sym.0.clone(), UdonInt::Sym(file.gensym(&sym.0)));
                Ok(())
            }
            KU2Instruction::Undef(sym) => {
                self.equates.remove(&sym.0);
                Ok(())
            }
            // -- instructions --
            KU2Instruction::NOP => self.assemble_op(file, &kudoninfo::opcodes::NOP, &[]),
            KU2Instruction::Push(o1) => self.assemble_op(file, &kudoninfo::opcodes::PUSH, &[o1]),
            KU2Instruction::Pop => self.assemble_op(file, &kudoninfo::opcodes::POP, &[]),
            KU2Instruction::JumpIfFalse(o1) => {
                self.assemble_op(file, &kudoninfo::opcodes::JUMP_IF_FALSE, &[o1])
            }
            KU2Instruction::Jump(o1) => self.assemble_op(file, &kudoninfo::opcodes::JUMP, &[o1]),
            KU2Instruction::Extern(o1) => {
                self.assemble_op(file, &kudoninfo::opcodes::EXTERN, &[o1])
            }
            KU2Instruction::Annotation(o1) => {
                self.assemble_op(file, &kudoninfo::opcodes::ANNOTATION, &[o1])
            }
            KU2Instruction::JumpIndirect(o1) => {
                self.assemble_op(file, &kudoninfo::opcodes::JUMP_INDIRECT, &[o1])
            }
            KU2Instruction::Copy => self.assemble_op(file, &kudoninfo::opcodes::COPY, &[]),
            KU2Instruction::Stop => {
                file.code.push(UdonInt::Op(&kudoninfo::opcodes::JUMP));
                file.code.push(UdonInt::I(0xFFFFFFFC));
                Ok(())
            }
            KU2Instruction::CopyStatic(a, b) => {
                self.assemble_op(file, &kudoninfo::opcodes::PUSH, &[a])?;
                self.assemble_op(file, &kudoninfo::opcodes::PUSH, &[b])?;
                self.assemble_op(file, &kudoninfo::opcodes::COPY, &[])
            }
            KU2Instruction::Ext(id, params) => {
                for param in params {
                    self.assemble_op(file, &kudoninfo::opcodes::PUSH, &[param])?;
                }
                let operand = KU2Operand::Sym(id.clone(), vec![]);
                self.assemble_op(file, &kudoninfo::opcodes::EXTERN, &[&operand])
            }
        }
    }

    /// Assembles a single line of code.
    /// This prefixes errors with the file/line.
    pub fn assemble(
        &mut self,
        file: &mut UdonProgram,
        loc: &str,
        instr: &KU2Instruction,
    ) -> Result<(), String> {
        self.assemble_no_line_info(file, loc, instr)
            .map_err(|v| format!("{}: {}", loc, v))
    }

    /// Ends the current package if necessary.
    pub fn end_package(&mut self) {
        if let Some(pkg) = self.editing_package.take() {
            let name = pkg.name.clone();
            self.packages.insert(name, pkg);
        }
    }

    /// Assembles a file.
    pub fn assemble_file(
        &mut self,
        file: &mut UdonProgram,
        srcname: &str,
        instrs: &[(usize, KU2Instruction)],
    ) -> Result<(), String> {
        self.end_package();
        for v in instrs {
            self.assemble(file, &format!("{}:{}", srcname, v.0), &v.1)?;
        }
        self.end_package();
        Ok(())
    }

    // -- Librarian --

    /// Invokes a snippet/package.
    /// Note dependencies are NOT handled here -- use [install_deps] at an appropriate time.
    pub fn snippet_invoke(
        &mut self,
        file: &mut UdonProgram,
        pkg_name: &str,
        snippet_equates: Option<Vec<(String, UdonInt)>>,
    ) -> Result<(), String> {
        if let Some(pkg) = self.packages.remove_entry(pkg_name) {
            let backup = if let Some(params) = snippet_equates {
                let r = Some(self.equates.clone());
                for v in params {
                    self.equates.insert(v.0, v.1);
                }
                r
            } else {
                None
            };
            let mut res = Ok(());
            for line in &pkg.1.contents {
                let res_l = self.assemble(file, &line.0, &line.1);
                if let Err(err) = res_l {
                    res = Err(format!("package {}: {}", pkg_name, err));
                    break;
                }
            }
            // restore equates on completion
            if let Some(backup) = backup {
                self.equates = backup;
            }
            self.packages.insert(pkg.0, pkg.1);
            res
        } else {
            Err(format!("Package '{}' unavailable", pkg_name))
        }
    }

    /// Installs the dependencies of the given package.
    pub fn install_deps(&mut self, file: &mut UdonProgram, pkg: &str) -> Result<(), String> {
        let deps = if let Some(pkg) = self.packages.get(pkg) {
            Ok(pkg.deps.clone())
        } else {
            Err(format!(
                "Can't install dependencies of package '{}' as it doesn't exist.",
                pkg
            ))
        }?;
        for v in deps {
            self.install(file, &v)?;
        }
        Ok(())
    }

    /// If the given package is not already installed, installs it, including dependencies if needed, immediately.
    pub fn install(&mut self, file: &mut UdonProgram, pkg: &str) -> Result<(), String> {
        if self.installed_packages.contains(pkg) {
            return Ok(());
        }
        self.installed_packages.insert(pkg.to_string());

        let deps = if let Some(pkg) = self.packages.get(pkg) {
            Ok(pkg.deps.clone())
        } else {
            Err(format!(
                "Can't install package '{}' as it doesn't exist.",
                pkg
            ))
        }?;
        // install dependencies
        for v in deps {
            self.install(file, &v)?;
        }
        // invoke the package itself
        self.snippet_invoke(file, pkg, None)
    }
}
