//! `kudonasm` is a replacement assembly language for Udon.

use kudonast::{UdonAccess, UdonHeapSlot, UdonInt, UdonProgram, UdonSymbol};
use kudoninfo::{UdonTypeRef, udonexternlookup_exi, udonexternlookup_exop};
use std::collections::{BTreeMap, BTreeSet};

mod parsing;
pub use parsing::*;

mod equate_stack;
pub use equate_stack::*;

mod heapconv;

// molly-guarded
pub mod decoded;

use decoded::{KU2DecInstr, KU2LeafInstr};

/// Represents a loaded package/snippet.
#[derive(Clone, Debug)]
pub struct KU2Package {
    pub name: String,
    pub deps: BTreeSet<String>,
    pub contents: Vec<(String, KU2DecInstr<'static>)>,
}

/// Builder for snippet invocations.
#[derive(Clone, Debug, Default)]
pub struct KU2CallBuilder {
    pub equates: Vec<(String, UdonInt)>,
    pub macro_args: Vec<UdonInt>,
}

impl KU2CallBuilder {
    pub fn with(mut self, s: &str, v: UdonInt) -> Self {
        self.equates.push((s.to_string(), v));
        self
    }
}

#[derive(Clone)]
pub struct KU2Context {
    /// The equate stack.
    pub equate_stack: KU2EquateStack,
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
        Self {
            equate_stack: Default::default(),
            installed_packages: Default::default(),
            packages: Default::default(),
            editing_package: None,
        }
    }
}

impl KU2Context {
    /// Handles equate symbol remap.
    fn ku2sym_to_udon(&self, v: &KU2Symbol) -> Result<String, String> {
        if let Some(equ) = self.equate_stack.get(&v.0) {
            if let UdonInt::Sym(sym) = &*equ {
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

    fn create_label(
        &self,
        internal_syms: &mut BTreeMap<String, i64>,
        symtab: &mut Vec<UdonSymbol>,
        sym: &(std::borrow::Cow<'_, KU2Symbol>, Option<UdonAccess>),
        ty: Option<UdonTypeRef>,
        loc: i64,
    ) -> Result<(), String> {
        let sym_remap = self.ku2sym_to_udon(&sym.0)?;
        if internal_syms.contains_key(&sym_remap) {
            Err(format!(
                "internal symbol {} ({}) declared twice; to overlap code/data syms in the output, consider using rename_sym",
                sym_remap, sym.0.0
            ))
        } else {
            internal_syms.insert(sym_remap.clone(), loc);
            if let Some(acc) = sym.1 {
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

    /// Assembles a single instruction of code.
    /// This prefixes file/line to errors.
    pub fn assemble_decoded(
        &mut self,
        file: &mut UdonProgram,
        loc: &str,
        instr: &KU2DecInstr,
    ) -> Result<(), String> {
        self.assemble_decoded_nopfx(file, loc, instr)
            .map_err(|v| format!("{}: {}", loc, v))
    }

    pub fn assemble_decoded_nopfx(
        &mut self,
        file: &mut UdonProgram,
        loc: &str,
        instr: &KU2DecInstr,
    ) -> Result<(), String> {
        match instr {
            KU2DecInstr::Package { name, deps } => {
                self.end_package();
                let mut pkg = if let Some(pkg) = self.packages.remove(name.as_str()) {
                    pkg
                } else {
                    KU2Package {
                        name: name.to_string(),
                        deps: BTreeSet::new(),
                        contents: vec![],
                    }
                };
                for v in deps.iter() {
                    pkg.deps.insert(v.clone());
                }
                self.editing_package = Some(pkg);
                Ok(())
            }
            KU2DecInstr::PackageEnd => {
                self.end_package();
                Ok(())
            }
            KU2DecInstr::Leaf(leaf) => {
                if let Some(editing_package) = &mut self.editing_package {
                    // Editing a package, so append.
                    editing_package
                        .contents
                        .push((loc.to_string(), instr.ownify()));
                    return Ok(());
                } else {
                    self.assemble_leaf(file, leaf)
                }
            }
        }
    }

    /// IMMEDIATELY assembles a leaf instruction with no regard to package insert state/etc.
    fn assemble_leaf(
        &mut self,
        file: &mut UdonProgram,
        instr: &KU2LeafInstr,
    ) -> Result<(), String> {
        match instr {
            // -- decl --
            KU2LeafInstr::Var { val, label } => {
                let loc = file.data.len();
                let uhs: UdonHeapSlot = self.conv_heap_slot(file, val)?;
                file.data.push(uhs.clone());
                if let Some(label) = label {
                    self.create_label(
                        &mut file.internal_syms,
                        &mut file.data_syms,
                        label,
                        Some(uhs.0),
                        loc as i64,
                    )?;
                }
                Ok(())
            }
            KU2LeafInstr::Sync {
                sym,
                prop,
                synctype,
            } => {
                let sym = self.ku2sym_to_udon(&sym)?;
                let synctype: u64 = (*synctype).into();
                file.sync_metadata.push((sym, prop.to_string(), synctype));
                Ok(())
            }
            KU2LeafInstr::UpdateOrder(operand) => {
                file.update_order = self.operand_udonint(file, operand)?;
                Ok(())
            }
            KU2LeafInstr::NetEvent {
                subr,
                maxeps,
                params,
            } => {
                let mut parameters = vec![];
                for v in params.iter() {
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
            KU2LeafInstr::RenameSym(from, to) => {
                for v in &mut file.code_syms {
                    if v.name.eq(from.as_str()) {
                        v.name.clone_from(to);
                    }
                }
                for v in &mut file.data_syms {
                    if v.name.eq(from.as_str()) {
                        v.name.clone_from(to);
                    }
                }
                for v in &mut file.sync_metadata {
                    if v.0.eq(from.as_str()) {
                        v.0.clone_from(to);
                    }
                }
                for v in &mut file.network_call_metadata {
                    if v.name.eq(from.as_str()) {
                        v.name.clone_from(to);
                    }
                    for param in &mut v.parameters {
                        if param.0.eq(from.as_str()) {
                            param.0.clone_from(to);
                        }
                    }
                }
                Ok(())
            }
            // -- meta --
            KU2LeafInstr::Invoke { macr, params } => {
                let mut call_builder = KU2CallBuilder::default();
                for v in params.iter() {
                    call_builder.macro_args.push(self.operand_udonint(file, v)?);
                }
                self.snippet_invoke(file, &macr, Some(call_builder))
            }
            KU2LeafInstr::CodeComment(comm) => {
                UdonProgram::add_comment(&mut file.code_comments, file.code.len(), comm);
                Ok(())
            }
            KU2LeafInstr::DataComment(comm) => {
                UdonProgram::add_comment(&mut file.data_comments, file.data.len(), comm);
                Ok(())
            }
            KU2LeafInstr::EmptyLeaf => Ok(()),
            // -- codelabel --
            KU2LeafInstr::CodeLabel(label) => self.create_label(
                &mut file.internal_syms,
                &mut file.code_syms,
                label,
                None,
                (file.code.len() * 4) as i64,
            ),
            // -- equate --
            KU2LeafInstr::Equate { sym, operand, up } => {
                let value = self.operand_udonint(file, operand)?;
                self.equate_stack.write(sym, value, *up);
                Ok(())
            }
            KU2LeafInstr::Local(sym) => {
                self.equate_stack
                    .write(sym, UdonInt::Sym(file.gensym(sym)), false);
                Ok(())
            }
            KU2LeafInstr::Undef(sym) => {
                self.equate_stack.undef(sym);
                Ok(())
            }
            KU2LeafInstr::BlockPush => {
                self.equate_stack.push();
                Ok(())
            }
            KU2LeafInstr::BlockPop => self.equate_stack.pop(),
            // -- instructions --
            KU2LeafInstr::ISeq(seq) => {
                for v in seq {
                    file.code.push(kudonast::UdonInt::Op(v.0));
                    // for opr in &v.1 {
                    if let Some(opr) = &v.1 {
                        let res = self.operand_udonint(file, &opr)?;
                        file.code.push(res);
                    }
                }
                Ok(())
            }
            KU2LeafInstr::ExternInstance {
                this,
                method,
                params,
            } => {
                // In concept, the code generation of this is much the same as Ext.
                // The fundamental difference arises in extern resolution, which is substantially different.
                let this_resolved = self.operand_udonint(file, this)?;
                let this_type_resolved: String = Self::udonint_udontype(file, &this_resolved)?
                    .name
                    .to_string();
                let mut params_resolved: Vec<UdonInt> = Vec::new();
                let mut params_types_resolved: Vec<String> = Vec::new();
                for p in params.iter() {
                    let ui = self.operand_udonint(file, p)?;
                    let ty = Self::udonint_udontype(file, &ui)?.name.to_string();
                    params_resolved.push(ui);
                    params_types_resolved.push(ty);
                }
                let resolved =
                    udonexternlookup_exi(&this_type_resolved, method, &params_types_resolved);
                if resolved.len() == 0 {
                    return Err(format!(
                        "{}.{} {:?} did not resolve",
                        this_type_resolved, method, params_types_resolved
                    ));
                } else if resolved.len() > 1 {
                    return Err(format!(
                        "{}.{} {:?} was ambiguous",
                        this_type_resolved, method, params_types_resolved
                    ));
                }
                file.code
                    .push(kudonast::UdonInt::Op(&kudoninfo::opcodes::PUSH));
                file.code.push(this_resolved);
                for param in params_resolved {
                    file.code
                        .push(kudonast::UdonInt::Op(&kudoninfo::opcodes::PUSH));
                    file.code.push(param);
                }
                file.code.push(UdonInt::Op(&kudoninfo::opcodes::EXTERN));
                let tmp = UdonInt::Sym(file.ensure_string(&resolved[0].name, true));
                file.code.push(tmp);
                Ok(())
            }
            KU2LeafInstr::ExternOperator { method, params } => {
                let mut params_resolved: Vec<UdonInt> = Vec::new();
                let mut params_types_resolved: Vec<String> = Vec::new();
                for p in params.iter() {
                    let ui = self.operand_udonint(file, p)?;
                    let ty = Self::udonint_udontype(file, &ui)?.name.to_string();
                    params_resolved.push(ui);
                    params_types_resolved.push(ty);
                }
                let resolved = udonexternlookup_exop(method, &params_types_resolved);
                if resolved.len() == 0 {
                    return Err(format!(
                        "{} {:?} did not resolve",
                        method, params_types_resolved
                    ));
                } else if resolved.len() > 1 {
                    return Err(format!(
                        "{} {:?} was ambiguous",
                        method, params_types_resolved
                    ));
                }
                for param in params_resolved {
                    file.code
                        .push(kudonast::UdonInt::Op(&kudoninfo::opcodes::PUSH));
                    file.code.push(param);
                }
                file.code.push(UdonInt::Op(&kudoninfo::opcodes::EXTERN));
                let tmp = UdonInt::Sym(file.ensure_string(&resolved[0].name, true));
                file.code.push(tmp);
                Ok(())
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
        self.assemble_decoded(file, loc, &KU2DecInstr::from(instr))
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
    /// This is a frontend for elf2uasm etc. to use accepting non-decoded instructions.
    pub fn snippet_invoke_anonymous(
        &mut self,
        file: &mut UdonProgram,
        pkg_content: &[(String, KU2Instruction)],
        frame: Option<KU2CallBuilder>,
    ) -> Result<(), String> {
        let decoded: Vec<(String, KU2DecInstr)> = pkg_content
            .iter()
            .map(|v| (v.0.clone(), KU2DecInstr::from(&v.1)))
            .collect();
        self.snippet_invoke_anonymous_decoded(file, &decoded, frame)
    }

    /// Invokes a snippet/package.
    /// Note dependencies are NOT handled here -- use [install_deps] at an appropriate time.
    pub fn snippet_invoke_anonymous_decoded(
        &mut self,
        file: &mut UdonProgram,
        pkg_content: &[(String, KU2DecInstr)],
        frame: Option<KU2CallBuilder>,
    ) -> Result<(), String> {
        let backup = if let Some(frame) = frame {
            let target = self.equate_stack.push();
            for v in frame.equates {
                target
                    .map
                    .insert(v.0.clone(), std::rc::Rc::new(std::cell::RefCell::new(v.1)));
            }
            target.macro_args = frame.macro_args;
            Some(self.equate_stack.len())
        } else {
            None
        };
        let mut res = Ok(());
        for line in pkg_content {
            let res_l = self.assemble_decoded(file, &line.0, &line.1);
            if let Err(err) = res_l {
                res = Err(err);
                break;
            }
        }
        // restore equates on completion if possible
        if let Some(expected_len) = backup {
            if self.equate_stack.len() != expected_len && !res.is_ok() {
                res = Err(format!("equate stack mismatch"));
            }
            while self.equate_stack.len() >= expected_len {
                _ = self.equate_stack.pop();
            }
        }
        res
    }

    /// Invokes a snippet/package.
    /// Note dependencies are NOT handled here -- use [install_deps] at an appropriate time.
    pub fn snippet_invoke(
        &mut self,
        file: &mut UdonProgram,
        pkg_name: &str,
        frame: Option<KU2CallBuilder>,
    ) -> Result<(), String> {
        if let Some(pkg) = self.packages.remove_entry(pkg_name) {
            let mut res = Ok(());
            if let Err(err) = self.snippet_invoke_anonymous_decoded(file, &pkg.1.contents, frame) {
                res = Err(format!("package {}: {}", pkg_name, err));
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
