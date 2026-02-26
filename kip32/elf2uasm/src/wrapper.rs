#![allow(dead_code)]
#![allow(mismatched_lifetime_syntaxes)]

use kudonasm::KU2Context;
use kudonast::{UdonAccess, UdonHeapValue, UdonInt, UdonProgram, uasm_op};
use kudoninfo::UdonTypeRef;
use kudonodin::OdinIntType;
use std::cell::RefCell;

macro_rules! udon_ext {
    ($($id:ident ($($arg:ident)*) = $value:literal)*) => {
        pub struct Wrapper {
            pub ku2: RefCell<KU2Context>,
            pub asm: RefCell<UdonProgram>,
            $(
                pub $id: String,
            )*
        }
        impl Wrapper {
            pub fn new() -> Wrapper {
                let mut asm = UdonProgram::default();
                let mut ku2 = KU2Context::default();
                $(
                    let $id = asm.ensure_string($value, true);
                    ku2.equates.insert(format!("_vmext_{}", stringify!($id)), UdonInt::Sym($id.clone()));
                )*
                Wrapper {
                    ku2: RefCell::new(ku2),
                    asm: RefCell::new(asm),
                    $(
                        $id,
                    )*
                }
            }
            $(
                pub fn $id(&self$(, $arg: impl std::fmt::Display)*) {
                    $(
                        uasm_op!(self.asm.borrow_mut(), PUSH, $arg);
                    )*
                    uasm_op!(self.asm.borrow_mut(), EXTERN, self.$id);
                }
            )*
        }
    };
}

// this really ought to be a crate in itself, huh - refcell wrappers
macro_rules! asm_proxy {
    ($id:ident ( $($key:ident : $ty:ty),* ) -> $ret:ty) => {
        pub fn $id(&self $(, $key: $ty)*) -> $ret {
            self.asm().$id(
                $(
                    $key,
                )*
            )
        }
    };
    ($id:ident ( $($key:ident : $ty:ty),* )) => {
        pub fn $id(&self $(, $key: $ty)*) {
            self.asm().$id(
                $(
                    $key,
                )*
            )
        }
    };
}

impl Wrapper {
    pub fn asm(&self) -> std::cell::RefMut<UdonProgram> {
        self.asm.borrow_mut()
    }
    pub fn ku2(&self) -> std::cell::RefMut<KU2Context> {
        self.ku2.borrow_mut()
    }
    pub fn ensure_i32(&self, v: i32) -> String {
        self.asm().ensure_iconst(OdinIntType::Int, v as i64)
    }
    pub fn comment_c(&self, v: &str) {
        let mut asm = self.asm();
        let cl = asm.code.len();
        UdonProgram::add_comment(&mut asm.code_comments, cl, v);
    }
    pub fn comment_d(&self, v: &str) {
        let mut asm = self.asm();
        let cl = asm.data.len();
        UdonProgram::add_comment(&mut asm.data_comments, cl, v);
    }
    pub fn jump(&self, a: &str) {
        uasm_op!(self.asm(), JUMP, a);
    }
    pub fn jump_ui(&self, a: UdonInt) {
        self.asm().code.push(UdonInt::Op(&kudoninfo::opcodes::JUMP));
        self.asm().code.push(a);
    }
    pub fn copy_static(&self, a: &str, b: &str) {
        uasm_op!(self.asm(), PUSH, a);
        uasm_op!(self.asm(), PUSH, b);
        uasm_op!(self.asm(), COPY);
    }
    pub fn jump_if_false_static(&self, a: &str, b: &str) {
        uasm_op!(self.asm(), PUSH, a);
        uasm_op!(self.asm(), JUMP_IF_FALSE, b);
    }
    pub fn jump_if_false_static_ui(&self, a: &str, b: UdonInt) {
        uasm_op!(self.asm(), PUSH, a);
        self.asm()
            .code
            .push(UdonInt::Op(&kudoninfo::opcodes::JUMP_IF_FALSE));
        self.asm().code.push(b);
    }
    asm_proxy!(ensure_iconst(ty: OdinIntType, v: i64) -> String);
    asm_proxy!(declare_heap(name: &impl ToString, public: Option<UdonAccess>, ty: impl Into<UdonTypeRef>, val: impl Into<UdonHeapValue>) -> Result<(), String>);
    asm_proxy!(declare_heap_i(name: &impl ToString, public: Option<UdonAccess>, ty: OdinIntType, val: impl Into<UdonInt>) -> Result<(), String>);
    asm_proxy!(code_label(name: &impl ToString, public: Option<UdonAccess>) -> Result<(), String>);
}

udon_ext!(
    obj_equality(a b r) =
        "SystemObject.__op_Equality__SystemObject_SystemObject__SystemBoolean"
    i32_eq(a b r) =
        "SystemInt32.__op_Equality__SystemInt32_SystemInt32__SystemBoolean"
    i32_neq(a b r) =
        "SystemInt32.__op_Inequality__SystemInt32_SystemInt32__SystemBoolean"
    i32_ge(a b r) =
        "SystemInt32.__op_GreaterThanOrEqual__SystemInt32_SystemInt32__SystemBoolean"

    i32_lt(a b r) =
        "SystemInt32.__op_LessThan__SystemInt32_SystemInt32__SystemBoolean"
    i32_add(a b r) =
        "SystemInt32.__op_Addition__SystemInt32_SystemInt32__SystemInt32"
    i32_sub(a b r) =
        "SystemInt32.__op_Subtraction__SystemInt32_SystemInt32__SystemInt32"
    i32_xor(a b r) =
        "SystemInt32.__op_LogicalXor__SystemInt32_SystemInt32__SystemInt32"
    i32_or(a b r) =
        "SystemInt32.__op_LogicalOr__SystemInt32_SystemInt32__SystemInt32"
    i32_and(a b r) =
        "SystemInt32.__op_LogicalAnd__SystemInt32_SystemInt32__SystemInt32"
    i32_shl(a b r) =
        "SystemInt32.__op_LeftShift__SystemInt32_SystemInt32__SystemInt32"
    i32_shr(a b r) =
        "SystemInt32.__op_RightShift__SystemInt32_SystemInt32__SystemInt32"
    i32_mul(a b r) =
        "SystemInt32.__op_Multiplication__SystemInt32_SystemInt32__SystemInt32"
    // This extern is risky because it will error on negative numbers.
    // For this reason, it's only used in the indirect jump code.
    u32_fromi32(i r) = "SystemConvert.__ToUInt32__SystemInt32__SystemUInt32"
    u32_add(a b r) =
        "SystemUInt32.__op_Addition__SystemUInt32_SystemUInt32__SystemUInt32"
    // init, etc.
    base64_decode(i r) =
        "SystemConvert.__FromBase64String__SystemString__SystemByteArray"
    // readers (byte-array, offset)
    read_i32(a o r) =
        "SystemBitConverter.__ToInt32__SystemByteArray_SystemInt32__SystemInt32"
    // reader signed conversions
    i32_fromi8(i r) = "SystemConvert.__ToInt32__SystemSByte__SystemInt32"
    i32_fromi16(i r) = "SystemConvert.__ToInt32__SystemInt16__SystemInt32"
    // This thing
    i32_frombool(i r) = "SystemConvert.__ToInt32__SystemBoolean__SystemInt32"

    i32array_create(size r) =
        "SystemInt32Array.__ctor__SystemInt32__SystemInt32Array"
    i32array_get(i o r) =
        "SystemInt32Array.__Get__SystemInt32__SystemInt32"
    i32array_set(i o v) =
        "SystemInt32Array.__Set__SystemInt32_SystemInt32__SystemVoid"

    i16array_create(size r) =
        "SystemInt16Array.__ctor__SystemInt32__SystemInt16Array"
    i16array_get(i o r) =
        "SystemInt16Array.__Get__SystemInt32__SystemInt16"

    i8array_create(size r) =
        "SystemSByteArray.__ctor__SystemInt32__SystemSByteArray"
    i8array_get(i o r) =
        "SystemSByteArray.__Get__SystemInt32__SystemSByte"

    u8array_create(size r) =
        "SystemByteArray.__ctor__SystemInt32__SystemByteArray"
    bytearray_copy(i a o) =
        "SystemByteArray.__CopyTo__SystemArray_SystemInt32__SystemVoid"

    // Reader and writer are crazy code caused by SystemConvert getting way too stabby. You heard it here first...
    // BlockCopy kinda saved the project's performance.
    bcopy(source source_ofs dest dest_ofs bytes) =
        "SystemBuffer.__BlockCopy__SystemArray_SystemInt32_SystemArray_SystemInt32_SystemInt32__SystemVoid"
);
