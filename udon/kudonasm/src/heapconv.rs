use crate::{KU2ChainOp, KU2Context, KU2HeapSlot, KU2Operand};
use kudonast::{
    UdonAccess, UdonHeapSlot, UdonHeapValue, UdonInt, UdonProgram, odininttype_to_udontype,
};
use kudoninfo::{UdonTypeRef, udontyperef};
use kudonodin::{OdinIntType, OdinPrimitive};

impl KU2Context {
    /// Converts a heap slot from KU2 form to a proper heap slot.
    pub fn conv_heap_slot(
        &mut self,
        file: &mut UdonProgram,
        val: &KU2HeapSlot,
    ) -> Result<UdonHeapSlot, String> {
        macro_rules! pmap {
            ($udon_type:ident, $variant:ident, $v:expr) => {
                Ok(UdonHeapSlot(
                    udontyperef!($udon_type),
                    UdonHeapValue::P(OdinPrimitive::$variant($v.clone())),
                ))
            };
        }
        macro_rules! imap {
            ($variant:ident, $v:expr) => {
                Ok(UdonHeapSlot(
                    kudonast::odininttype_to_udontype(OdinIntType::$variant),
                    UdonHeapValue::I(OdinIntType::$variant, UdonInt::I($v)),
                ))
            };
        }
        macro_rules! icmap {
            ($variant:ident, $v:expr) => {
                Ok(UdonHeapSlot(
                    kudonast::odininttype_to_udontype(OdinIntType::$variant),
                    UdonHeapValue::I(OdinIntType::$variant, self.operand_udonint(file, $v)?),
                ))
            };
        }

        match val {
            KU2HeapSlot::String(v) => pmap!(SystemString, String, v),
            KU2HeapSlot::Type(v) => Ok(UdonHeapSlot(
                udontyperef!(SystemType),
                UdonHeapValue::RType(v.odin_name.to_string()),
            )),

            KU2HeapSlot::SByte(v) => imap!(SByte, *v),
            KU2HeapSlot::Byte(v) => imap!(Byte, *v),
            KU2HeapSlot::Short(v) => imap!(Short, *v),
            KU2HeapSlot::UShort(v) => imap!(UShort, *v),
            KU2HeapSlot::Int(v) => imap!(Int, *v),
            KU2HeapSlot::UInt(v) => imap!(UInt, *v),
            KU2HeapSlot::Long(v) => imap!(Long, *v),
            KU2HeapSlot::ULong(v) => imap!(ULong, *v),
            KU2HeapSlot::Bool(v) => imap!(Bool, *v),
            KU2HeapSlot::Char(v) => imap!(Char, *v as i64),

            KU2HeapSlot::SByteC(v) => icmap!(SByte, v),
            KU2HeapSlot::ByteC(v) => icmap!(Byte, v),
            KU2HeapSlot::ShortC(v) => icmap!(Short, v),
            KU2HeapSlot::UShortC(v) => icmap!(UShort, v),
            KU2HeapSlot::IntC(v) => icmap!(Int, v),
            KU2HeapSlot::UIntC(v) => icmap!(UInt, v),
            KU2HeapSlot::LongC(v) => icmap!(Long, v),
            KU2HeapSlot::ULongC(v) => icmap!(ULong, v),
            KU2HeapSlot::BoolC(v) => icmap!(Bool, v),
            KU2HeapSlot::CharC(v) => icmap!(Char, v),

            KU2HeapSlot::Float(v) => pmap!(SystemSingle, Float, v),
            KU2HeapSlot::Double(v) => pmap!(SystemDouble, Double, v),

            KU2HeapSlot::True => pmap!(SystemBoolean, Bool, true),
            KU2HeapSlot::False => pmap!(SystemBoolean, Bool, false),

            KU2HeapSlot::Null(ot) => Ok(UdonHeapSlot(
                ot.clone(),
                UdonHeapValue::P(OdinPrimitive::Null),
            )),

            KU2HeapSlot::This(ot) => Ok(UdonHeapSlot(ot.clone(), UdonHeapValue::This)),
            KU2HeapSlot::Custom(ot, ov) => Ok(UdonHeapSlot(ot.clone(), ov.clone())),
        }
    }

    pub fn conv_heap_const_int(
        &mut self,
        file: &mut UdonProgram,
        ot: OdinIntType,
        v: &KU2Operand,
    ) -> Result<UdonInt, String> {
        let val = self.operand_udonint(file, v)?;
        if let Ok(early_resolve) = val.resolve(&file.internal_syms) {
            Ok(UdonInt::Sym(file.ensure_iconst(ot, early_resolve)))
        } else {
            let sym = file.gensym("late_iconst");
            let pos = file.data.len();
            UdonProgram::add_comment(
                &mut file.data_comments,
                pos,
                &format!("immediate operand: {:?}", val),
            );
            file.declare_heap(
                &sym,
                Some(UdonAccess::Elidable),
                odininttype_to_udontype(ot),
                UdonHeapValue::I(ot, val),
            )?;
            Ok(UdonInt::Sym(sym))
        }
    }

    /// **Use only for operands!**
    /// Like conv_heap_slot, but opportunistically makes use of constants.
    pub fn conv_heap_const(
        &mut self,
        file: &mut UdonProgram,
        val: &KU2HeapSlot,
    ) -> Result<UdonInt, String> {
        match val {
            KU2HeapSlot::String(v) => Ok(UdonInt::Sym(file.ensure_string(&v, false))),

            KU2HeapSlot::SByte(v) => {
                self.conv_heap_const_int(file, OdinIntType::SByte, &KU2Operand::Raw(*v))
            }
            KU2HeapSlot::Byte(v) => {
                self.conv_heap_const_int(file, OdinIntType::Byte, &KU2Operand::Raw(*v))
            }
            KU2HeapSlot::Short(v) => {
                self.conv_heap_const_int(file, OdinIntType::Short, &KU2Operand::Raw(*v))
            }
            KU2HeapSlot::UShort(v) => {
                self.conv_heap_const_int(file, OdinIntType::UShort, &KU2Operand::Raw(*v))
            }
            KU2HeapSlot::Int(v) => {
                self.conv_heap_const_int(file, OdinIntType::Int, &KU2Operand::Raw(*v))
            }
            KU2HeapSlot::UInt(v) => {
                self.conv_heap_const_int(file, OdinIntType::UInt, &KU2Operand::Raw(*v))
            }
            KU2HeapSlot::Long(v) => {
                self.conv_heap_const_int(file, OdinIntType::Long, &KU2Operand::Raw(*v))
            }
            KU2HeapSlot::ULong(v) => {
                self.conv_heap_const_int(file, OdinIntType::ULong, &KU2Operand::Raw(*v))
            }
            KU2HeapSlot::Bool(v) => {
                self.conv_heap_const_int(file, OdinIntType::Bool, &KU2Operand::Raw(*v))
            }
            KU2HeapSlot::Char(v) => {
                self.conv_heap_const_int(file, OdinIntType::Char, &KU2Operand::Raw(*v as i64))
            }

            KU2HeapSlot::SByteC(v) => self.conv_heap_const_int(file, OdinIntType::SByte, v),
            KU2HeapSlot::ByteC(v) => self.conv_heap_const_int(file, OdinIntType::Byte, v),
            KU2HeapSlot::ShortC(v) => self.conv_heap_const_int(file, OdinIntType::Short, v),
            KU2HeapSlot::UShortC(v) => self.conv_heap_const_int(file, OdinIntType::UShort, v),
            KU2HeapSlot::IntC(v) => self.conv_heap_const_int(file, OdinIntType::Int, v),
            KU2HeapSlot::UIntC(v) => self.conv_heap_const_int(file, OdinIntType::UInt, v),
            KU2HeapSlot::LongC(v) => self.conv_heap_const_int(file, OdinIntType::Long, v),
            KU2HeapSlot::ULongC(v) => self.conv_heap_const_int(file, OdinIntType::ULong, v),
            KU2HeapSlot::BoolC(v) => self.conv_heap_const_int(file, OdinIntType::Bool, v),
            KU2HeapSlot::CharC(v) => self.conv_heap_const_int(file, OdinIntType::Char, v),

            KU2HeapSlot::True => {
                self.conv_heap_const_int(file, OdinIntType::Bool, &KU2Operand::Raw(1))
            }
            KU2HeapSlot::False => {
                self.conv_heap_const_int(file, OdinIntType::Bool, &KU2Operand::Raw(0))
            }

            _ => {
                let slot = self.conv_heap_slot(file, val)?;
                let sym = file.gensym("immediate");
                let pos = file.data.len();
                UdonProgram::add_comment(
                    &mut file.data_comments,
                    pos,
                    &format!("immediate operand: {:?}", val),
                );
                file.declare_heap(&sym, Some(UdonAccess::Elidable), slot.0, slot.1)?;
                Ok(UdonInt::Sym(sym))
            }
        }
    }

    /// Parses an operand to [UdonInt].
    pub fn operand_udonint(
        &mut self,
        file: &mut UdonProgram,
        v: &KU2Operand,
    ) -> Result<UdonInt, String> {
        match v {
            KU2Operand::Sym(sym) => {
                if let Some(equ) = self.equate_stack.get(&sym.0) {
                    // Existing equate
                    Ok(equ.clone())
                } else {
                    Ok(UdonInt::Sym(sym.0.clone()))
                }
            }
            KU2Operand::HeapConst(heap_slot) => self.conv_heap_const(file, heap_slot),
            KU2Operand::Raw(v) => Ok(UdonInt::I(*v)),
            KU2Operand::ChainOp(KU2ChainOp::Add, a, b) => Ok(UdonInt::Add(
                Box::new(self.operand_udonint(file, a)?),
                Box::new(self.operand_udonint(file, b)?),
            )),
            KU2Operand::ChainOp(KU2ChainOp::Sub, a, b) => Ok(UdonInt::Sub(
                Box::new(self.operand_udonint(file, a)?),
                Box::new(self.operand_udonint(file, b)?),
            )),
            KU2Operand::ChainOp(KU2ChainOp::Mul, a, b) => Ok(UdonInt::Mul(
                Box::new(self.operand_udonint(file, a)?),
                Box::new(self.operand_udonint(file, b)?),
            )),
            KU2Operand::Ext(ext) => Ok(UdonInt::Sym(file.ensure_string(ext, true))),
            KU2Operand::Ord(ord) => Ok(UdonInt::I(*ord as i64)),
            KU2Operand::Arg(arg) => {
                if let Some(equ) = self.equate_stack.top().macro_args.get(*arg) {
                    Ok(equ.clone())
                } else {
                    Err(format!("Macro arg {} did not exist", arg))
                }
            }
        }
    }

    /// Attempts to map an UdonInt to an UdonType.
    pub fn udonint_udontype<'f>(
        file: &'f UdonProgram,
        ui: &UdonInt,
    ) -> Result<&'f UdonTypeRef, String> {
        let resolved = ui.resolve(&file.internal_syms)?;
        if resolved < 0 || (resolved as usize) >= file.data.len() {
            Err(format!(
                "Parameter {:?} resolved to invalid heap index {}; no type available.",
                ui, resolved
            ))
        } else {
            Ok(&file.data[resolved as usize].0)
        }
    }
}
