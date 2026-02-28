#![allow(dead_code)]

use std::cell::RefCell;
use std::fmt::Display;
use std::fmt::Write;

/// Udon Assembly program wrapper.
/// This is built immutably in order to allow for chained calls onto the writer (which happen often).
#[derive(Clone, Default)]
pub struct UASMWriter {
    pub data: RefCell<String>,
    pub code: RefCell<String>,
}

impl Display for UASMWriter {
    fn fmt(&self, f: &mut std::fmt::Formatter<'_>) -> std::fmt::Result {
        f.write_str(".data_start\n\n")?;
        f.write_str(&self.data.borrow())?;
        f.write_str("\n.data_end\n")?;
        f.write_str(".code_start\n\n")?;
        f.write_str(&self.code.borrow())?;
        f.write_str("\n.code_end\n")?;
        Ok(())
    }
}

impl UASMWriter {
    pub fn declare_heap(&self, id: &str, ut: &str, ival: &str, export: bool, line_suffix: &str) {
        if export {
            writeln!(self.data.borrow_mut(), "\t.export {}", id).unwrap();
        }
        writeln!(
            self.data.borrow_mut(),
            "\t{}: %{}, {}{}",
            id,
            ut,
            ival,
            line_suffix
        )
        .unwrap();
    }

    // -- Writer Wrapping --

    /// Writes to the code block.
    pub fn code(&self, v: impl std::fmt::Display) {
        writeln!(self.code.borrow_mut(), "{}", v).unwrap();
    }
    /// Writes to the data block.
    pub fn data(&self, v: impl std::fmt::Display) {
        writeln!(self.data.borrow_mut(), "{}", v).unwrap();
    }

    // -- Instructions --

    /// NOP instruction.
    pub fn nop(&self) {
        writeln!(self.code.borrow_mut(), "\tNOP").unwrap();
    }

    /// PUSH instruction.
    pub fn push(&self, index: impl std::fmt::Display) {
        writeln!(self.code.borrow_mut(), "\tPUSH, {}", index).unwrap();
    }

    /// POP instruction.
    pub fn pop(&self) {
        writeln!(self.code.borrow_mut(), "\tPOP").unwrap();
    }

    /// JUMP_IF_FALSE instruction.
    pub fn jump_if_false(&self, target: impl std::fmt::Display) {
        writeln!(self.code.borrow_mut(), "\tJUMP_IF_FALSE, {}", target).unwrap();
    }

    /// JUMP instruction.
    pub fn jump(&self, target: impl std::fmt::Display) {
        writeln!(self.code.borrow_mut(), "\tJUMP, {}", target).unwrap();
    }

    /// EXTERN instruction.
    pub fn ext(&self, index: impl std::fmt::Display) {
        writeln!(self.code.borrow_mut(), "\tEXTERN, {}", index).unwrap();
    }

    /// ANNOTATION instruction.
    pub fn annotation(&self, v: impl std::fmt::Display) {
        writeln!(self.code.borrow_mut(), "\tANNOTATION, {}", v).unwrap();
    }

    /// JUMP_INDIRECT instruction.
    pub fn jump_indirect(&self, index: impl std::fmt::Display) {
        writeln!(self.code.borrow_mut(), "\tJUMP_INDIRECT, {}", index).unwrap();
    }

    /// COPY instruction.
    pub fn copy(&self) {
        writeln!(self.code.borrow_mut(), "\tCOPY").unwrap();
    }

    // -- Idioms --

    pub fn stop(&self) {
        self.code("\tJUMP, 0xFFFFFFFC");
    }

    pub fn copy_static(&self, from: impl std::fmt::Display, to: impl std::fmt::Display) {
        self.push(from);
        self.push(to);
        self.copy();
    }

    pub fn jump_if_false_static(&self, test: impl std::fmt::Display, to: impl std::fmt::Display) {
        self.push(test);
        self.jump_if_false(to);
    }

    pub fn code_label(&self, id: impl std::fmt::Display, export: bool, line_suffix: &str) {
        if export {
            writeln!(self.code.borrow_mut(), ".export {}", id).unwrap();
        }
        writeln!(self.code.borrow_mut(), "{}:{}", id, line_suffix).unwrap();
    }
}
