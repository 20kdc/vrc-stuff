use kudoninfo::UdonType;
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
pub enum KU2Access {
    #[serde(rename = "internal")]
    Internal,
    #[serde(rename = "symbol")]
    Symbol,
    #[serde(rename = "public")]
    Public,
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

/// Use when this is an operand.
/// Forms:
/// * `[symbol]`
/// * `"string"`
/// * `1234`
#[derive(Clone, Debug, PartialEq, Eq, PartialOrd, Ord)]
pub enum KU2Operand {
    Sym(KU2Symbol),
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
            let b: Option<KU2Symbol> = seq.next_element()?;
            if b.is_some() {
                Err(serde::de::Error::custom(
                    "Label reference should only have the label.",
                ))
            } else {
                Ok(KU2Operand::Sym(a))
            }
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

/// Instruction/pseudoinstruction enum.
#[derive(Clone, Debug, PartialEq, PartialOrd, Deserialize)]
pub enum KU2Instruction {
    #[serde(rename = "var")]
    Var(KU2Symbol, KU2Access, UdonType, kudonast::UdonHeapValue),
    #[serde(rename = "sync")]
    Sync(KU2Symbol, KU2Symbol, KU2SyncType),
    #[serde(rename = "internal")]
    CodeInternal(KU2Symbol),
    #[serde(rename = "_")]
    CodeInternalAlt(KU2Symbol),
    #[serde(rename = "symbol")]
    CodeSymbol(KU2Symbol),
    #[serde(rename = "public")]
    CodePublic(KU2Symbol),
    #[serde(rename = "equ")]
    Equate(KU2Symbol, kudonast::UdonInt),
    #[serde(rename = "equ_extern")]
    EquateExtern(KU2Symbol, String),
    #[serde(rename = "update_order")]
    UpdateOrder(KU2Operand),
    #[serde(rename = "net_event")]
    NetEvent(KU2Symbol, i32, Vec<(KU2Symbol, UdonType)>),
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
}

#[cfg(test)]
#[test]
fn ku2instruction_parse_test() {
    let r1: KU2Instruction = ron::from_str("_(Test)").unwrap();
    assert_eq!(
        r1,
        KU2Instruction::CodeInternalAlt(KU2Symbol("Test".to_string()))
    );
}

/// Parsing. This focuses on implementing line by line reading on top of RON by essentially fudging the JavaScript semicolon trick.
/// In the error case, performance gets worse and worse for longer bodies, so, er. don't commit errors I guess.'
pub fn parse(src: &str) -> Result<Vec<KU2Instruction>, ron::error::SpannedError> {
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
                bank.clear();
                line_number_offset += banked_lines;
                banked_lines = 0;
                instructions.push(ok);
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
    let card = r###"
/*
 * data and code can mix freely
 * symbols aren't quoted
 * access is divided into 'internal', 'symbol' and 'public'
 * newlines are used as implicit separators
 */
var (message, public, "SystemString", P(String("hello
world")))
sync (message, this, none)
// equates expose UdonInt ops
equ (example, Add(I(1), I(2)))
// or can be used to shorthand externs
equ_extern (ext_log, "UnityEngineDebug.__Log__SystemObject__SystemVoid")
// code labels use the access as their opening keyword, except 'internal' can also be '_'
public(_interact)
    // instruction operands are:
    // * [label]
    // * "constant string/extern"
    // * 0x1234 // numbers
    push ([message])
    extern ([ext_log])
    jump (0xFFFFFFFC)
_(infinite_loop)
    // parsing will add in each line until a complete entity is found
    // this can lead to unexpected errors if i.e. commas are missing
    jump ([
        infinite_loop,
    ])
// assorted
update_order (0)
net_event (Test, 5, [(Test_Param1, "SystemString")])
"###;
    let _v: Vec<KU2Instruction> = parse(card).unwrap();
}
