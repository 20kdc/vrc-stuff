use kudoninfo::{UdonType, UdonTypeRef};
use serde::{Deserialize, Serialize, de::Visitor};

#[derive(Clone, Copy, Debug, PartialEq, Eq, PartialOrd, Ord, Serialize, Deserialize)]
pub enum KU2SyncType {
    #[serde(rename = "none")]
    None,
    #[serde(rename = "linear")]
    Linear,
    #[serde(rename = "smooth")]
    Smooth,
    #[serde(rename = "custom")]
    Custom(u64),
}

impl Into<u64> for KU2SyncType {
    fn into(self) -> u64 {
        match self {
            Self::None => kudoninfo::interpolations::NONE,
            Self::Linear => kudoninfo::interpolations::LINEAR,
            Self::Smooth => kudoninfo::interpolations::SMOOTH,
            Self::Custom(v) => v,
        }
    }
}

#[derive(Clone, Copy, Debug, PartialEq, Eq, PartialOrd, Ord, Serialize, Deserialize)]
pub enum KU2StringAffinity {
    #[serde(rename = "data")]
    Data,
    #[serde(rename = "extern")]
    Extern,
    #[serde(rename = "char")]
    Char,
    #[serde(rename = "error")]
    Error,
}

/// Use when this is definitely a symbol.
#[derive(Clone, Debug, PartialEq, Eq, PartialOrd, Ord)]
pub struct KU2Symbol(pub String);
struct KU2SymbolVisitor;
impl<'de> Visitor<'de> for KU2SymbolVisitor {
    type Value = KU2Symbol;
    fn expecting(&self, formatter: &mut std::fmt::Formatter) -> std::fmt::Result {
        write!(formatter, "KU2Symbol")
    }
    fn visit_str<E>(self, v: &str) -> Result<Self::Value, E>
    where
        E: serde::de::Error,
    {
        Ok(KU2Symbol(v.to_string()))
    }
}
impl<'de> Deserialize<'de> for KU2Symbol {
    fn deserialize<D>(deserializer: D) -> Result<Self, D::Error>
    where
        D: serde::Deserializer<'de>,
    {
        deserializer.deserialize_identifier(KU2SymbolVisitor)
    }
}
impl Serialize for KU2Symbol {
    fn serialize<S>(&self, serializer: S) -> Result<S::Ok, S::Error>
    where
        S: serde::Serializer,
    {
        serializer.serialize_str(&self.0)
    }
}

#[cfg(test)]
#[test]
fn ku2symbol_parse_test() {
    let r1: KU2Symbol = ron::from_str("Test").unwrap();
    assert_eq!(r1, KU2Symbol("Test".to_string()));
}

#[derive(Clone, Debug, PartialEq, Eq, PartialOrd, Ord, Deserialize)]
pub enum KU2Modifier {
    #[serde(rename = "add")]
    Add(KU2Operand),
    #[serde(rename = "sub")]
    Sub(KU2Operand),
    #[serde(rename = "mul")]
    Mul(KU2Operand),
    #[serde(rename = "heap_const")]
    HeapConst(kudonodin::OdinIntType),
}

/// Use when this is an operand.
/// Forms:
/// * `[symbol]`
/// * `"string"`
/// * `1234`
#[derive(Clone, Debug, PartialEq, Eq, PartialOrd, Ord)]
pub enum KU2Operand {
    Sym(KU2Symbol, Vec<KU2Modifier>),
    Str(String),
    I(i64),
}
struct KU2ExprVisitor;
impl<'de> Visitor<'de> for KU2ExprVisitor {
    type Value = KU2Operand;
    fn expecting(&self, formatter: &mut std::fmt::Formatter) -> std::fmt::Result {
        write!(formatter, "KU2Operand")
    }
    fn visit_str<E>(self, v: &str) -> Result<Self::Value, E>
    where
        E: serde::de::Error,
    {
        Ok(KU2Operand::Str(v.to_string()))
    }
    fn visit_i64<E>(self, v: i64) -> Result<Self::Value, E>
    where
        E: serde::de::Error,
    {
        Ok(KU2Operand::I(v as i64))
    }
    fn visit_u64<E>(self, v: u64) -> Result<Self::Value, E>
    where
        E: serde::de::Error,
    {
        Ok(KU2Operand::I(v as i64))
    }
    fn visit_seq<A>(self, mut seq: A) -> Result<Self::Value, A::Error>
    where
        A: serde::de::SeqAccess<'de>,
    {
        let a: Option<KU2Symbol> = seq.next_element()?;
        if let Some(a) = a {
            let mut total = Vec::new();
            while let Some(modifier) = seq.next_element()? as Option<KU2Modifier> {
                total.push(modifier);
            }
            Ok(KU2Operand::Sym(a, total))
        } else {
            Err(serde::de::Error::custom(
                "Label reference without reference.",
            ))
        }
    }
}
impl<'de> Deserialize<'de> for KU2Operand {
    fn deserialize<D>(deserializer: D) -> Result<Self, D::Error>
    where
        D: serde::Deserializer<'de>,
    {
        deserializer.deserialize_any(KU2ExprVisitor)
    }
}

/// Heap slots contents.
/// Reference [kudonodin::OdinPrimitive]
#[derive(Clone, Debug, PartialEq, PartialOrd, Deserialize)]
pub enum KU2HeapSlot {
    // --
    // Decimal(OdinDecimal),
    #[serde(rename = "string")]
    String(String),
    // WTF16(Vec<u16>),
    // Guid(OdinGuid),
    #[serde(rename = "sbyte")]
    SByte(KU2Operand),
    #[serde(rename = "byte")]
    Byte(KU2Operand),
    #[serde(rename = "short")]
    Short(KU2Operand),
    #[serde(rename = "ushort")]
    UShort(KU2Operand),
    #[serde(rename = "int")]
    Int(KU2Operand),
    #[serde(rename = "uint")]
    UInt(KU2Operand),
    #[serde(rename = "long")]
    Long(KU2Operand),
    #[serde(rename = "ulong")]
    ULong(KU2Operand),
    #[serde(rename = "float")]
    Float(f32),
    #[serde(rename = "double")]
    Double(f64),
    #[serde(rename = "char")]
    Char(KU2Operand),
    // boolean
    #[serde(rename = "true")]
    True,
    #[serde(rename = "false")]
    False,
    // ExternalRefGuid(OdinGuid),
    // ExternalRefString(String),
    // }
    #[serde(rename = "null")]
    Null(UdonTypeRef),
    // --
    #[serde(rename = "this")]
    This(UdonTypeRef),
    #[serde(rename = "ast")]
    Custom(UdonTypeRef, kudonast::UdonHeapValue),
}

/// Instruction/pseudoinstruction enum.
#[derive(Clone, Debug, PartialEq, PartialOrd, Deserialize)]
pub enum KU2Instruction {
    // decl
    #[serde(rename = "var_internal")]
    VarInternal(KU2Symbol, KU2HeapSlot),
    #[serde(rename = "var_symbol")]
    #[serde(alias = "var")]
    VarSymbol(KU2Symbol, KU2HeapSlot),
    #[serde(rename = "var_public")]
    VarPublic(KU2Symbol, KU2HeapSlot),
    #[serde(rename = "sync")]
    Sync(KU2Symbol, KU2SyncType),
    #[serde(rename = "sync_prop")]
    SyncProp(KU2Symbol, String, KU2SyncType),
    #[serde(rename = "update_order")]
    UpdateOrder(KU2Operand),
    #[serde(rename = "net_event")]
    NetEvent(KU2Symbol, i32, Vec<(KU2Symbol, UdonType)>),
    #[serde(rename = "rename_sym")]
    RenameSym(KU2Symbol, String),
    // meta
    #[serde(rename = "package")]
    Package(String, Vec<String>),
    #[serde(rename = "code_comment")]
    CodeComment(String),
    #[serde(rename = "data_comment")]
    DataComment(String),
    // codelabel
    #[serde(rename = "internal")]
    #[serde(alias = "_")]
    CodeInternal(KU2Symbol),
    #[serde(rename = "symbol")]
    CodeSymbol(KU2Symbol),
    #[serde(rename = "public")]
    CodePublic(KU2Symbol),
    // equate
    #[serde(rename = "equ")]
    EquateInt(KU2Symbol, KU2Operand),
    #[serde(rename = "equ_str")]
    EquateStr(KU2Symbol, KU2StringAffinity, KU2Operand),
    #[serde(rename = "local")]
    Local(KU2Symbol),
    #[serde(rename = "undef")]
    Undef(KU2Symbol),
    // instructions
    #[serde(rename = "nop")]
    NOP,
    #[serde(rename = "push")]
    Push(KU2Operand),
    #[serde(rename = "pop")]
    Pop,
    #[serde(rename = "jump_if_false")]
    JumpIfFalse(KU2Operand),
    #[serde(rename = "jump")]
    Jump(KU2Operand),
    #[serde(rename = "extern")]
    Extern(KU2Operand),
    #[serde(rename = "annotation")]
    Annotation(KU2Operand),
    #[serde(rename = "jump_indirect")]
    JumpIndirect(KU2Operand),
    #[serde(rename = "copy")]
    Copy,
    // macroinstructions
    #[serde(rename = "stop")]
    Stop,
    #[serde(rename = "copy_static")]
    CopyStatic(KU2Operand, KU2Operand),
    #[serde(rename = "ext")]
    Ext(KU2Symbol, Vec<KU2Operand>),
}

#[cfg(test)]
#[test]
fn ku2instruction_parse_test() {
    let r1: KU2Instruction = ron::from_str("_(Test)").unwrap();
    assert_eq!(
        r1,
        KU2Instruction::CodeInternal(KU2Symbol("Test".to_string()))
    );
}

/// Parsing. This focuses on implementing line by line reading on top of RON by essentially fudging the JavaScript semicolon trick.
/// In the error case, performance gets worse and worse for longer bodies, so, er. don't commit errors I guess.'
pub fn kudonasm_parse(src: &str) -> Result<Vec<(usize, KU2Instruction)>, ron::error::SpannedError> {
    let mut instructions = Vec::new();
    let mut line_number_offset: usize = 0;
    let mut bank = String::new();
    let mut banked_lines: usize = 0;
    let mut last_error: Option<ron::error::SpannedError> = None;
    for line in src.split("\n") {
        // add line to bank
        bank.push_str(line);
        bank.push_str("\n");
        banked_lines += 1;
        // try parsing
        let res: ron::error::SpannedResult<KU2Instruction> = ron::from_str(&bank);
        last_error = match res {
            Err(mut err) => {
                err.span.start.line += line_number_offset;
                err.span.end.line += line_number_offset;
                // To try and prevent performance problems, abort immediately if the error doesn't make sense for what we're doing.
                match err.code {
                    ron::Error::Eof => {
                        // obviously fine
                    }
                    ron::Error::UnclosedBlockComment => {}
                    ron::Error::UnclosedLineComment => {}
                    ron::Error::ExpectedStringEnd => {}
                    _ => {
                        return Err(err);
                    }
                }
                Some(err)
            }
            Ok(ok) => {
                let lno = line_number_offset;
                bank.clear();
                line_number_offset += banked_lines;
                banked_lines = 0;
                instructions.push((lno + 1, ok));
                None
            }
        };
    }
    let mut is_whitespace = true;
    for chr in bank.as_bytes() {
        if *chr > 32 {
            is_whitespace = false;
            break;
        }
    }
    if is_whitespace {
        Ok(instructions)
    } else if let Some(err) = last_error {
        Err(err)
    } else {
        Ok(instructions)
    }
}

#[cfg(test)]
#[test]
fn ku2parsing_card() {
    let card = include_str!("card.ron");
    let _v: Vec<(usize, KU2Instruction)> = kudonasm_parse(card).unwrap();
}
