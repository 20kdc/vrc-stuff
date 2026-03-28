//! The fusion module implements instruction fusion and syscall inference.
//! In order to do this, it must operate on Sci32Image.

use crate::*;

/// This implements instructions that are >1 word.
/// This includes nametable syscalls and fused instructions.
#[derive(Clone, PartialEq, Eq, Debug)]
pub struct Kip32FusedInstr {
    pub content: Kip32FIC,
    /// If content does not branch, jump to this address.
    /// Note that JAL has an embedded value for RD -- don't use this for that, as it risks corrupting i.e. JAL-PIC.
    pub jump: u32,
    /// This is true if it is safe to assume jump == PC+4.
    /// Notably, this is not the same as if jump is in fact PC+4; just that falling through to PC+4 will have the same effect.
    /// In practice, this only occurs if the recompiler supports fallthrough.
    /// Regarding what happens if content is a jump instruction: Jump instructions branch, so fallthrough is not considered.
    pub fallthrough_ok: bool,
}

/// Fused instruction content.
#[derive(Clone, PartialEq, Eq, Debug)]
pub enum Kip32FIC {
    I(Sci32Instr),
    /// Nametable syscall, written as EBREAK followed by the address of a constant C-string.
    NametableSyscall(String),
}

impl Kip32FusedInstr {
    /// Reads a fusable-instruction from the image.
    /// Importantly, this doesn't do fusion by itself.
    /// It does, however, convert forced jump instructions into metadata-marked NOP instructions.
    pub fn read_nofuse(img: &Sci32Image, pc: u32) -> Self {
        let word_idx = (pc >> 2) as usize;
        let pcp4 = pc.wrapping_add(4);
        let istr = Sci32Instr::decode(pc, img.data[word_idx]);

        // EBREAK syscall
        if img.is_instruction_at(pcp4) {
            if let Sci32Instr::EBREAK = istr {
                let addr = img.data[word_idx + 1];
                let name = img.read_metadata_syscall(addr);
                if let Some(name) = name {
                    return Self {
                        content: Kip32FIC::NametableSyscall(name),
                        jump: pc.wrapping_add(8),
                        fallthrough_ok: false,
                    };
                }
            }
        }

        // Call-Into-Data nametable syscall
        if let Sci32Instr::JumpAndLink {
            rd: Kip32Reg::RA,
            rd_value,
            value,
        } = &istr
        {
            if !img.is_instruction_at(*value)
                && let Some(cstr) = img.read_metadata_syscall(*value)
            {
                return Self {
                    content: Kip32FIC::NametableSyscall(cstr),
                    jump: *rd_value,
                    fallthrough_ok: pcp4 == *rd_value,
                };
            }
        }

        match istr {
            Sci32Instr::JumpAndLink {
                rd: Kip32Reg::Zero,
                rd_value: _,
                value,
            } => Self {
                content: Kip32FIC::I(Sci32Instr::NOP(Kip32NOPSource::Fusion)),
                jump: value,
                fallthrough_ok: false,
            },
            _ => Self {
                content: Kip32FIC::I(istr),
                jump: pcp4,
                fallthrough_ok: true,
            },
        }
    }

    /// Attempts to fuse B into A.
    /// Keep in mind the following:
    /// * B **must** be at the location of A.jump!
    /// * B will be generated anyway, in case it's jumped directly into.
    /// * The returned instruction is either self or a replacement for self.
    pub fn try_fuse(self, b: Kip32FusedInstr) -> (bool, Kip32FusedInstr) {
        match (self, b) {
            // -- jump fusion --
            (
                Kip32FusedInstr {
                    content: istr,
                    jump: _,
                    fallthrough_ok,
                },
                Kip32FusedInstr {
                    content: Kip32FIC::I(Sci32Instr::NOP(_)),
                    jump,
                    fallthrough_ok: _,
                },
            ) => {
                // We pull fallthrough_ok from instruction A, because if true then we know it naturally falls through into the jump instruction B.
                (
                    true,
                    Self {
                        content: istr,
                        jump,
                        fallthrough_ok,
                    },
                )
            }
            // -- fail fusion --
            (a, _) => (false, a),
        }
    }

    /// Reads an instruction, attempting to perform fusion.
    pub fn read_fuse(img: &Sci32Image, pc: u32) -> Self {
        let ia = Self::read_nofuse(img, pc);
        if ia.jump < (img.instructions * 4) as u32 {
            let ib = Self::read_nofuse(img, ia.jump);
            ia.try_fuse(ib).1
        } else {
            ia
        }
    }
}
