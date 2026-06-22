//! This 'decoded representation' eats a lot of the syntactic sugar and allows for better use of patterns.

use crate::{KU2HeapSlot, KU2Instruction, KU2Operand, KU2Symbol, KU2SyncType};
use kudonast::UdonAccess;
use kudoninfo::{UdonOpcode, UdonTypeRef};
use std::borrow::Cow;

/// 'Leaf instruction' (doesn't get reordered/etc.)
/// Not stable, even by this crate's standards.
#[derive(Clone, Debug)]
pub enum KU2LeafInstr<'parent> {
    // decl
    Var {
        val: Cow<'parent, KU2HeapSlot>,
        label: Option<(Cow<'parent, KU2Symbol>, Option<UdonAccess>)>,
    },
    Sync {
        sym: Cow<'parent, KU2Symbol>,
        prop: Cow<'parent, String>,
        synctype: KU2SyncType,
    },
    NetEvent {
        subr: Cow<'parent, KU2Symbol>,
        maxeps: i32,
        params: Cow<'parent, Vec<(KU2Symbol, UdonTypeRef)>>,
    },
    UpdateOrder(Cow<'parent, KU2Operand>),
    RenameSym(Cow<'parent, String>, Cow<'parent, String>),
    // meta
    Invoke {
        macr: Cow<'parent, String>,
        params: Cow<'parent, Vec<KU2Operand>>,
    },
    CodeComment(Cow<'parent, String>),
    DataComment(Cow<'parent, String>),
    EmptyLeaf,
    // codelabel
    CodeLabel((Cow<'parent, KU2Symbol>, Option<UdonAccess>)),
    // equate
    Equate {
        sym: Cow<'parent, String>,
        operand: Cow<'parent, KU2Operand>,
        up: bool,
    },
    Local(Cow<'parent, String>),
    Undef(Cow<'parent, String>),
    BlockPush,
    BlockPop,
    // instructions / macroinstructions
    ISeq(Vec<(&'static UdonOpcode, Option<Cow<'parent, KU2Operand>>)>),
    ExternInstance {
        this: Cow<'parent, KU2Operand>,
        method: Cow<'parent, String>,
        params: Cow<'parent, Vec<KU2Operand>>,
    },
    ExternOperator {
        method: Cow<'parent, String>,
        params: Cow<'parent, Vec<KU2Operand>>,
    },
}

/// 'Decoded instruction'.
/// Not stable, even by this crate's standards.
#[derive(Clone, Debug)]
pub enum KU2DecInstr<'parent> {
    Package {
        name: Cow<'parent, String>,
        deps: Cow<'parent, Vec<String>>,
    },
    PackageEnd,
    Leaf(KU2LeafInstr<'parent>),
}

fn own<'x, T: Clone>(src: &Cow<'x, T>) -> Cow<'static, T> {
    Cow::Owned((**src).clone())
}
fn b<'x, T: Clone>(src: &'x T) -> Cow<'x, T> {
    Cow::Borrowed(src)
}

impl<'parent> KU2DecInstr<'parent> {
    pub fn from(value: &'parent KU2Instruction) -> Self {
        match value {
            // -- decl --
            KU2Instruction::VarInternal(sym, val) => Self::Leaf(KU2LeafInstr::Var {
                val: b(val),
                label: Some((b(sym), None)),
            }),
            KU2Instruction::VarElidable(sym, val) => Self::Leaf(KU2LeafInstr::Var {
                val: b(val),
                label: Some((b(sym), Some(UdonAccess::Elidable))),
            }),
            KU2Instruction::VarSymbol(sym, val) => Self::Leaf(KU2LeafInstr::Var {
                val: b(val),
                label: Some((b(sym), Some(UdonAccess::Symbol))),
            }),
            KU2Instruction::VarPublic(sym, val) => Self::Leaf(KU2LeafInstr::Var {
                val: b(val),
                label: Some((b(sym), Some(UdonAccess::Public))),
            }),
            KU2Instruction::Sync(sym, synctype) => Self::Leaf(KU2LeafInstr::Sync {
                sym: b(sym),
                prop: Cow::Owned("this".to_string()),
                synctype: *synctype,
            }),
            KU2Instruction::SyncProp(sym, prop, synctype) => Self::Leaf(KU2LeafInstr::Sync {
                sym: b(sym),
                prop: b(prop),
                synctype: *synctype,
            }),
            KU2Instruction::UpdateOrder(operand) => {
                Self::Leaf(KU2LeafInstr::UpdateOrder(b(operand)))
            }
            KU2Instruction::NetEvent(subr, maxeps, params) => Self::Leaf(KU2LeafInstr::NetEvent {
                subr: b(subr),
                maxeps: *maxeps,
                params: b(params),
            }),
            KU2Instruction::RenameSym(from, to) => {
                Self::Leaf(KU2LeafInstr::RenameSym(b(&from.0), b(to)))
            }
            // -- meta --
            KU2Instruction::Package(name, deps) => Self::Package {
                name: Cow::Borrowed(name),
                deps: Cow::Borrowed(deps),
            },
            KU2Instruction::PackageEnd => Self::PackageEnd,
            KU2Instruction::Invoke(name, params) => Self::Leaf(KU2LeafInstr::Invoke {
                macr: b(name),
                params: b(params),
            }),
            KU2Instruction::CodeComment(comm) => Self::Leaf(KU2LeafInstr::CodeComment(b(comm))),
            KU2Instruction::DataComment(comm) => Self::Leaf(KU2LeafInstr::DataComment(b(comm))),
            KU2Instruction::EOF => Self::Leaf(KU2LeafInstr::EmptyLeaf),
            // -- codelabel --
            KU2Instruction::CodeInternal(sym) => {
                Self::Leaf(KU2LeafInstr::CodeLabel((b(sym), None)))
            }
            KU2Instruction::CodeSymbol(sym) => {
                Self::Leaf(KU2LeafInstr::CodeLabel((b(sym), Some(UdonAccess::Symbol))))
            }
            KU2Instruction::CodeElidable(sym) => Self::Leaf(KU2LeafInstr::CodeLabel((
                b(sym),
                Some(UdonAccess::Elidable),
            ))),
            KU2Instruction::CodePublic(sym) => {
                Self::Leaf(KU2LeafInstr::CodeLabel((b(sym), Some(UdonAccess::Public))))
            }
            // -- equate --
            KU2Instruction::EquateInt(sym, operand) => Self::Leaf(KU2LeafInstr::Equate {
                sym: b(&sym.0),
                operand: b(operand),
                up: false,
            }),
            KU2Instruction::EquateUp(sym, operand) => Self::Leaf(KU2LeafInstr::Equate {
                sym: b(&sym.0),
                operand: b(operand),
                up: true,
            }),
            KU2Instruction::Local(sym) => Self::Leaf(KU2LeafInstr::Local(b(&sym.0))),
            KU2Instruction::Undef(sym) => Self::Leaf(KU2LeafInstr::Undef(b(&sym.0))),
            KU2Instruction::BlockPush => Self::Leaf(KU2LeafInstr::BlockPush),
            KU2Instruction::BlockPop => Self::Leaf(KU2LeafInstr::BlockPop),
            // -- instructions --
            KU2Instruction::NOP => {
                Self::Leaf(KU2LeafInstr::ISeq(vec![(&kudoninfo::opcodes::NOP, None)]))
            }
            KU2Instruction::Push(o1) => Self::Leaf(KU2LeafInstr::ISeq(vec![(
                &kudoninfo::opcodes::PUSH,
                Some(b(o1)),
            )])),
            KU2Instruction::Pop => {
                Self::Leaf(KU2LeafInstr::ISeq(vec![(&kudoninfo::opcodes::POP, None)]))
            }
            KU2Instruction::JumpIfFalse(o1) => Self::Leaf(KU2LeafInstr::ISeq(vec![(
                &kudoninfo::opcodes::JUMP_IF_FALSE,
                Some(b(o1)),
            )])),
            KU2Instruction::Jump(o1) => Self::Leaf(KU2LeafInstr::ISeq(vec![(
                &kudoninfo::opcodes::JUMP,
                Some(b(o1)),
            )])),
            KU2Instruction::Extern(o1) => Self::Leaf(KU2LeafInstr::ISeq(vec![(
                &kudoninfo::opcodes::EXTERN,
                Some(b(o1)),
            )])),
            KU2Instruction::Annotation(o1) => Self::Leaf(KU2LeafInstr::ISeq(vec![(
                &kudoninfo::opcodes::ANNOTATION,
                Some(b(o1)),
            )])),
            KU2Instruction::JumpIndirect(o1) => Self::Leaf(KU2LeafInstr::ISeq(vec![(
                &kudoninfo::opcodes::JUMP_INDIRECT,
                Some(b(o1)),
            )])),
            KU2Instruction::Copy => {
                Self::Leaf(KU2LeafInstr::ISeq(vec![(&kudoninfo::opcodes::COPY, None)]))
            }
            KU2Instruction::Stop => Self::Leaf(KU2LeafInstr::ISeq(vec![(
                &kudoninfo::opcodes::JUMP,
                Some(Cow::Owned(KU2Operand::Raw(0xFFFFFFFC))),
            )])),
            // -- macroinstructions --
            KU2Instruction::CopyStatic(src, dst) => Self::Leaf(KU2LeafInstr::ISeq(vec![
                (&kudoninfo::opcodes::PUSH, Some(b(src))),
                (&kudoninfo::opcodes::PUSH, Some(b(dst))),
                (&kudoninfo::opcodes::COPY, None),
            ])),
            KU2Instruction::CopyStaticAlt(dst, src) => Self::Leaf(KU2LeafInstr::ISeq(vec![
                (&kudoninfo::opcodes::PUSH, Some(b(src))),
                (&kudoninfo::opcodes::PUSH, Some(b(dst))),
                (&kudoninfo::opcodes::COPY, None),
            ])),
            KU2Instruction::JumpIfFalseStatic(bl, jt) => Self::Leaf(KU2LeafInstr::ISeq(vec![
                (&kudoninfo::opcodes::PUSH, Some(b(bl))),
                (&kudoninfo::opcodes::JUMP_IF_FALSE, Some(b(jt))),
            ])),
            KU2Instruction::Ext(operand, params) => {
                let mut vec = Vec::new();
                for param in params {
                    vec.push((&kudoninfo::opcodes::PUSH, Some(b(param))));
                }
                vec.push((&kudoninfo::opcodes::EXTERN, Some(b(operand))));
                Self::Leaf(KU2LeafInstr::ISeq(vec))
            }
            KU2Instruction::ExternInstance(this, extname, params) => {
                Self::Leaf(KU2LeafInstr::ExternInstance {
                    this: b(this),
                    method: b(extname),
                    params: b(params),
                })
            }
            KU2Instruction::ExternOperator(extname, params) => {
                Self::Leaf(KU2LeafInstr::ExternOperator {
                    method: b(extname),
                    params: b(params),
                })
            }
        }
    }
    pub fn ownify(&self) -> KU2DecInstr<'static> {
        match self {
            Self::Package { name, deps } => KU2DecInstr::Package {
                name: own(name),
                deps: own(deps),
            },
            Self::PackageEnd => KU2DecInstr::PackageEnd,
            Self::Leaf(leaf) => KU2DecInstr::Leaf(leaf.ownify()),
        }
    }
}

impl<'parent> KU2LeafInstr<'parent> {
    pub fn ownify(&self) -> KU2LeafInstr<'static> {
        match self {
            Self::Var { val, label } => KU2LeafInstr::Var {
                val: own(val),
                label: label.as_ref().map(|v| (own(&v.0), v.1)),
            },
            Self::Sync {
                sym,
                prop,
                synctype,
            } => KU2LeafInstr::Sync {
                sym: own(sym),
                prop: own(prop),
                synctype: *synctype,
            },
            Self::NetEvent {
                subr,
                maxeps,
                params,
            } => KU2LeafInstr::NetEvent {
                subr: own(subr),
                maxeps: *maxeps,
                params: own(params),
            },
            Self::UpdateOrder(operand) => KU2LeafInstr::UpdateOrder(own(operand)),
            Self::RenameSym(from, to) => KU2LeafInstr::RenameSym(own(from), own(to)),
            Self::Invoke { macr, params } => KU2LeafInstr::Invoke {
                macr: own(macr),
                params: own(params),
            },
            Self::CodeComment(comm) => KU2LeafInstr::CodeComment(own(comm)),
            Self::DataComment(comm) => KU2LeafInstr::DataComment(own(comm)),
            Self::EmptyLeaf => KU2LeafInstr::EmptyLeaf,
            Self::CodeLabel(label) => KU2LeafInstr::CodeLabel((own(&label.0), label.1)),
            Self::Equate { sym, operand, up } => KU2LeafInstr::Equate {
                sym: own(sym),
                operand: own(operand),
                up: *up,
            },
            Self::Local(sym) => KU2LeafInstr::Local(own(sym)),
            Self::Undef(sym) => KU2LeafInstr::Undef(own(sym)),
            Self::BlockPush => KU2LeafInstr::BlockPush,
            Self::BlockPop => KU2LeafInstr::BlockPop,
            Self::ISeq(seq) => KU2LeafInstr::ISeq(
                seq.iter()
                    .map(|v| (v.0, v.1.as_ref().map(|opr| own(&opr))))
                    .collect(),
            ),
            Self::ExternInstance {
                this,
                method,
                params,
            } => KU2LeafInstr::ExternInstance {
                this: own(this),
                method: own(method),
                params: own(params),
            },
            Self::ExternOperator { method, params } => KU2LeafInstr::ExternOperator {
                method: own(method),
                params: own(params),
            },
        }
    }
}
