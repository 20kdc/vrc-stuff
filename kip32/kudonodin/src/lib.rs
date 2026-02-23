//! This crate _probably_ isn't a Derivative Work of OdinSerializer <https://github.com/TeamSirenix/odin-serializer/>
//!
//! However, if OdinSerializer disagrees with this assessment, in good faith, the license header is reproduced here:
//!
//! ```text
//! Copyright (c) 2018 Sirenix IVS
//!
//! Licensed under the Apache License, Version 2.0 (the "License");
//! you may not use this file except in compliance with the License.
//! You may obtain a copy of the License at
//!
//!     http://www.apache.org/licenses/LICENSE-2.0
//!
//! Unless required by applicable law or agreed to in writing, software
//! distributed under the License is distributed on an "AS IS" BASIS,
//! WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//! See the License for the specific language governing permissions and
//! limitations under the License.
//! ```

use serde::{Deserialize, Serialize};
use std::io::{Read, Write};

#[cfg(test)]
mod tests;

// Our main strategy here is to, above all else, **NOT** follow OdinSerializer's API style, since that would make the code a 1:1 translation.
// We handle the same format, and we're similar where obviously necessary, but we're implementing the OdinSerializer _format_ in our own way.

/// Odin 'GUIDs' are really Unity GUIDs, and are thus 8 bytes.
pub type OdinGuid = [u8; 8];
pub type OdinDecimal = [u8; 8];

/// Primitive value, suitable for using in both AST and entry forms.
/// Note that we consider GUID and String external references 'primitive', but not indexed.
/// This is an intentional design choice; while it isn't used this way, it's clear the design intends Unity GUIDs to fit here.
#[derive(Clone, Debug, PartialOrd, PartialEq, Serialize, Deserialize)]
pub enum OdinPrimitive {
    Decimal(OdinDecimal),
    String(String),
    Guid(OdinGuid),
    SByte(i8),
    Byte(u8),
    Short(i16),
    UShort(u16),
    Int(i32),
    UInt(u32),
    Long(i64),
    ULong(u64),
    Float(f32),
    Double(f64),
    Char(u16),
    Boolean(bool),
    // Note that VRC doesn't actually set these up {
    ExternalRefGuid(OdinGuid),
    ExternalRefString(String),
    // }
    Null,
}

#[derive(Clone, Debug, PartialOrd, Ord, PartialEq, Eq, Serialize, Deserialize)]
pub enum OdinTypeEntry {
    Null,
    TypeName(i32, String),
    TypeID(i32),
}

/// Anything with named/unnamed variants.
#[derive(Clone, Debug, PartialOrd, PartialEq, Serialize, Deserialize)]
pub enum OdinEntryValue {
    StartRefNode(OdinTypeEntry, i32),
    StartStructNode(OdinTypeEntry),
    InternalRef(i32),
    ExternalRefIdx(i32),
    Primitive(OdinPrimitive),
}

#[derive(Clone, Debug, PartialOrd, PartialEq, Serialize, Deserialize)]
pub enum OdinEntry {
    Value(Option<String>, OdinEntryValue),
    EndOfNode,
    StartOfArray(i64),
    EndOfArray,
    /// length in elements, bytes per element, data
    PrimitiveArray(i32, i32, Vec<u8>),
    EndOfStream,
}

fn read_fixed<const N: usize>(src: &mut impl Read) -> std::io::Result<[u8; N]> {
    let mut dat: [u8; N] = [0; N];
    src.read_exact(&mut dat)?;
    Ok(dat)
}

macro_rules! read_int {
    ($o_goner:expr, $o_title:ty) => {
        <$o_title>::from_le_bytes(read_fixed($o_goner)?)
    };
}

fn read_vec(src: &mut impl Read, sz: usize) -> std::io::Result<Vec<u8>> {
    let mut alloced: Vec<u8> = Vec::new();
    alloced.resize(sz as usize, 0);
    src.read_exact(&mut alloced)?;
    Ok(alloced)
}

pub fn odin_write_string_value(target: &mut impl Write, sv: &str) -> std::io::Result<()> {
    // The Udon writer doesn't seem to ever use 8-bit strings.
    target.write_all(&[1])?;
    let res: Vec<u16> = sv.encode_utf16().collect();
    target.write_all(&(res.len() as u32).to_le_bytes())?;
    for v in res {
        target.write_all(&v.to_le_bytes())?;
    }
    Ok(())
}

pub fn odin_read_string_value(src: &mut impl Read) -> std::io::Result<String> {
    let flag = read_int!(src, u8);
    let strlen = read_int!(src, u32);
    if flag == 0 {
        // utf-8
        Ok(String::from_utf8_lossy(&read_vec(src, strlen as usize)?).into())
    } else {
        // utf-16
        let mut vec: Vec<u16> = Vec::new();
        for _ in 0..strlen {
            vec.push(read_int!(src, u16));
        }
        Ok(String::from_utf16_lossy(&vec))
    }
}

fn odin_write_name_opt(
    target: &mut impl Write,
    name: &Option<String>,
    has_name: u8,
) -> std::io::Result<()> {
    let no_name = has_name + 1;
    if let Some(name) = name {
        target.write_all(&[has_name])?;
        odin_write_string_value(target, &name)
    } else {
        target.write_all(&[no_name])
    }
}

pub fn odin_write_type_entry(target: &mut impl Write, te: &OdinTypeEntry) -> std::io::Result<()> {
    match te {
        OdinTypeEntry::Null => target.write_all(&[0x2E]),
        OdinTypeEntry::TypeName(id, name) => {
            target.write_all(&[0x2F])?;
            target.write_all(&id.to_le_bytes())?;
            odin_write_string_value(target, name)
        }
        OdinTypeEntry::TypeID(id) => {
            target.write_all(&[0x30])?;
            target.write_all(&id.to_le_bytes())
        }
    }
}

pub fn odin_read_type_entry(src: &mut impl Read) -> std::io::Result<OdinTypeEntry> {
    let entry_type = read_int!(src, u8);
    match entry_type {
        0x2E => Ok(OdinTypeEntry::Null),
        0x2F => {
            let id = read_int!(src, i32);
            Ok(OdinTypeEntry::TypeName(id, odin_read_string_value(src)?))
        }
        0x30 => Ok(OdinTypeEntry::TypeID(read_int!(src, i32))),
        _ => Err(std::io::Error::new(
            std::io::ErrorKind::InvalidData,
            "invalid TE type",
        )),
    }
}

macro_rules! ser_prim {
    ($target:expr, $name:expr, $b: expr, $val:literal) => {{
        odin_write_name_opt($target, $name, $val)?;
        $target.write_all(&$b.to_le_bytes())?;
    }};
}

macro_rules! des_uval {
    ($val:expr) => {
        Ok(OdinEntry::Value(None, $val))
    };
}
macro_rules! des_nval {
    ($src:expr, $val:expr) => {{
        let name = Some(odin_read_string_value($src)?);
        Ok(OdinEntry::Value(name, $val))
    }};
}

macro_rules! des_uprim {
    ($prim:expr) => {
        des_uval!(OdinEntryValue::Primitive($prim))
    };
}
macro_rules! des_nprim {
    ($src:expr, $prim:expr) => {
        des_nval!($src, OdinEntryValue::Primitive($prim))
    };
}

impl OdinEntry {
    /// Writes this entry to the given writer.
    pub fn write(&self, target: &mut impl Write) -> std::io::Result<()> {
        match self {
            Self::Value(name, OdinEntryValue::StartRefNode(ty, id)) => {
                odin_write_name_opt(target, name, 0x01)?;
                odin_write_type_entry(target, ty)?;
                target.write_all(&id.to_le_bytes())?;
            }
            Self::Value(name, OdinEntryValue::StartStructNode(ty)) => {
                odin_write_name_opt(target, name, 0x03)?;
                odin_write_type_entry(target, ty)?;
            }
            // --
            Self::EndOfNode => {
                target.write_all(&[0x05])?;
            }
            Self::StartOfArray(len) => {
                target.write_all(&[0x06])?;
                target.write_all(&len.to_le_bytes())?;
            }
            Self::EndOfArray => {
                target.write_all(&[0x07])?;
            }
            Self::PrimitiveArray(len, es, arr) => {
                target.write_all(&[0x08])?;
                target.write_all(&len.to_le_bytes())?;
                target.write_all(&es.to_le_bytes())?;
                target.write_all(arr)?;
            }
            // --
            Self::Value(name, OdinEntryValue::InternalRef(id)) => {
                odin_write_name_opt(target, name, 0x09)?;
                target.write_all(&id.to_le_bytes())?;
            }
            Self::Value(name, OdinEntryValue::ExternalRefIdx(id)) => {
                odin_write_name_opt(target, name, 0x0B)?;
                target.write_all(&id.to_le_bytes())?;
            }
            // --
            Self::Value(name, OdinEntryValue::Primitive(OdinPrimitive::ExternalRefGuid(guid))) => {
                odin_write_name_opt(target, name, 0x0D)?;
                target.write_all(guid)?;
            }
            Self::Value(name, OdinEntryValue::Primitive(OdinPrimitive::SByte(b))) => {
                ser_prim!(target, name, b, 0x0F)
            }
            Self::Value(name, OdinEntryValue::Primitive(OdinPrimitive::Byte(b))) => {
                ser_prim!(target, name, b, 0x11)
            }
            Self::Value(name, OdinEntryValue::Primitive(OdinPrimitive::Short(b))) => {
                ser_prim!(target, name, b, 0x13)
            }
            Self::Value(name, OdinEntryValue::Primitive(OdinPrimitive::UShort(b))) => {
                ser_prim!(target, name, b, 0x15)
            }
            Self::Value(name, OdinEntryValue::Primitive(OdinPrimitive::Int(b))) => {
                ser_prim!(target, name, b, 0x17)
            }
            Self::Value(name, OdinEntryValue::Primitive(OdinPrimitive::UInt(b))) => {
                ser_prim!(target, name, b, 0x19)
            }
            Self::Value(name, OdinEntryValue::Primitive(OdinPrimitive::Long(b))) => {
                ser_prim!(target, name, b, 0x1B)
            }
            Self::Value(name, OdinEntryValue::Primitive(OdinPrimitive::ULong(b))) => {
                ser_prim!(target, name, b, 0x1D)
            }
            Self::Value(name, OdinEntryValue::Primitive(OdinPrimitive::Float(b))) => {
                ser_prim!(target, name, b, 0x1F)
            }
            Self::Value(name, OdinEntryValue::Primitive(OdinPrimitive::Double(b))) => {
                ser_prim!(target, name, b, 0x21)
            }
            Self::Value(name, OdinEntryValue::Primitive(OdinPrimitive::Decimal(b))) => {
                odin_write_name_opt(target, name, 0x23)?;
                target.write_all(b)?;
            }
            Self::Value(name, OdinEntryValue::Primitive(OdinPrimitive::Char(b))) => {
                ser_prim!(target, name, b, 0x25)
            }
            Self::Value(name, OdinEntryValue::Primitive(OdinPrimitive::String(b))) => {
                odin_write_name_opt(target, name, 0x27)?;
                odin_write_string_value(target, &b)?;
            }
            Self::Value(name, OdinEntryValue::Primitive(OdinPrimitive::Guid(b))) => {
                odin_write_name_opt(target, name, 0x29)?;
                target.write_all(b)?;
            }
            Self::Value(name, OdinEntryValue::Primitive(OdinPrimitive::Boolean(b))) => {
                odin_write_name_opt(target, name, 0x2B)?;
                if *b {
                    target.write_all(&[0x01])?;
                } else {
                    target.write_all(&[0x00])?;
                }
            }
            Self::Value(name, OdinEntryValue::Primitive(OdinPrimitive::Null)) => {
                odin_write_name_opt(target, name, 0x2D)?;
            }
            // --
            // type name, id 0x2f, 0x30
            Self::EndOfStream => {
                target.write_all(&[0x31])?;
            }
            // --
            Self::Value(name, OdinEntryValue::Primitive(OdinPrimitive::ExternalRefString(b))) => {
                odin_write_name_opt(target, name, 0x32)?;
                odin_write_string_value(target, &b)?;
            }
        }
        Ok(())
    }
    pub fn read(src: &mut impl Read) -> std::io::Result<Self> {
        let entry_type = read_int!(src, u8);
        match entry_type {
            0x01 => {
                let name = odin_read_string_value(src)?;
                let te = odin_read_type_entry(src)?;
                let id = read_int!(src, i32);
                Ok(OdinEntry::Value(
                    Some(name),
                    OdinEntryValue::StartRefNode(te, id),
                ))
            }
            0x02 => {
                let te = odin_read_type_entry(src)?;
                let id = read_int!(src, i32);
                Ok(OdinEntry::Value(None, OdinEntryValue::StartRefNode(te, id)))
            }
            0x03 => {
                let name = odin_read_string_value(src)?;
                let te = odin_read_type_entry(src)?;
                Ok(OdinEntry::Value(
                    Some(name),
                    OdinEntryValue::StartStructNode(te),
                ))
            }
            0x04 => {
                let te = odin_read_type_entry(src)?;
                Ok(OdinEntry::Value(None, OdinEntryValue::StartStructNode(te)))
            }
            // --
            0x05 => Ok(OdinEntry::EndOfNode),
            0x06 => Ok(OdinEntry::StartOfArray(read_int!(src, i64))),
            0x07 => Ok(OdinEntry::EndOfArray),
            0x08 => {
                let a = read_int!(src, i32);
                let b = read_int!(src, i32);
                if a < 0 || b < 0 {
                    return Err(std::io::Error::new(
                        std::io::ErrorKind::InvalidData,
                        "prim array size < 0",
                    ));
                }
                Ok(OdinEntry::PrimitiveArray(
                    a,
                    b,
                    read_vec(src, (a as usize) * (b as usize))?,
                ))
            }
            // --
            0x09 => des_nval!(src, OdinEntryValue::InternalRef(read_int!(src, i32))),
            0x0A => des_uval!(OdinEntryValue::InternalRef(read_int!(src, i32))),
            0x0B => des_nval!(src, OdinEntryValue::ExternalRefIdx(read_int!(src, i32))),
            0x0C => des_uval!(OdinEntryValue::ExternalRefIdx(read_int!(src, i32))),
            // --
            0x0D => des_nprim!(src, OdinPrimitive::ExternalRefGuid(read_fixed(src)?)),
            0x0E => des_uprim!(OdinPrimitive::ExternalRefGuid(read_fixed(src)?)),
            0x0F => des_nprim!(src, OdinPrimitive::SByte(read_int!(src, i8))),
            0x10 => des_uprim!(OdinPrimitive::SByte(read_int!(src, i8))),
            0x11 => des_nprim!(src, OdinPrimitive::Byte(read_int!(src, u8))),
            0x12 => des_uprim!(OdinPrimitive::Byte(read_int!(src, u8))),
            0x13 => des_nprim!(src, OdinPrimitive::Short(read_int!(src, i16))),
            0x14 => des_uprim!(OdinPrimitive::Short(read_int!(src, i16))),
            0x15 => des_nprim!(src, OdinPrimitive::UShort(read_int!(src, u16))),
            0x16 => des_uprim!(OdinPrimitive::UShort(read_int!(src, u16))),
            0x17 => des_nprim!(src, OdinPrimitive::Int(read_int!(src, i32))),
            0x18 => des_uprim!(OdinPrimitive::Int(read_int!(src, i32))),
            0x19 => des_nprim!(src, OdinPrimitive::UInt(read_int!(src, u32))),
            0x1A => des_uprim!(OdinPrimitive::UInt(read_int!(src, u32))),
            0x1B => des_nprim!(src, OdinPrimitive::Long(read_int!(src, i64))),
            0x1C => des_uprim!(OdinPrimitive::Long(read_int!(src, i64))),
            0x1D => des_nprim!(src, OdinPrimitive::ULong(read_int!(src, u64))),
            0x1E => des_uprim!(OdinPrimitive::ULong(read_int!(src, u64))),
            0x1F => des_nprim!(src, OdinPrimitive::Float(read_int!(src, f32))),
            0x20 => des_uprim!(OdinPrimitive::Float(read_int!(src, f32))),
            0x21 => des_nprim!(src, OdinPrimitive::Double(read_int!(src, f64))),
            0x22 => des_uprim!(OdinPrimitive::Double(read_int!(src, f64))),
            0x23 => des_nprim!(src, OdinPrimitive::Decimal(read_fixed(src)?)),
            0x24 => des_uprim!(OdinPrimitive::Decimal(read_fixed(src)?)),
            0x25 => des_nprim!(src, OdinPrimitive::Char(read_int!(src, u16))),
            0x26 => des_uprim!(OdinPrimitive::Char(read_int!(src, u16))),
            0x27 => des_nprim!(src, OdinPrimitive::String(odin_read_string_value(src)?)),
            0x28 => des_uprim!(OdinPrimitive::String(odin_read_string_value(src)?)),
            0x29 => des_nprim!(src, OdinPrimitive::Guid(read_fixed(src)?)),
            0x2A => des_uprim!(OdinPrimitive::Guid(read_fixed(src)?)),
            0x2B => des_nprim!(src, OdinPrimitive::Boolean(read_int!(src, u8) == 1)),
            0x2C => des_uprim!(OdinPrimitive::Boolean(read_int!(src, u8) == 1)),
            0x2D => des_nprim!(src, OdinPrimitive::Null),
            0x2E => des_uprim!(OdinPrimitive::Null),
            // --
            // type name, id 0x2f, 0x30
            0x31 => Ok(OdinEntry::EndOfStream),
            // --
            0x32 => des_nprim!(
                src,
                OdinPrimitive::ExternalRefString(odin_read_string_value(src)?)
            ),
            0x33 => des_uprim!(OdinPrimitive::ExternalRefString(odin_read_string_value(
                src
            )?)),
            // --
            _ => Err(std::io::Error::new(
                std::io::ErrorKind::InvalidData,
                "invalid entry type",
            )),
        }
    }
    /// Given a u8 slice, read all the entries.
    pub fn read_all_from_slice(mut resread: &[u8]) -> std::io::Result<Vec<OdinEntry>> {
        let mut entries = Vec::new();
        while resread.len() > 0 {
            entries.push(
                OdinEntry::read(&mut resread).expect("all entries should read properly"),
            );
        }
        Ok(entries)
    }
    /// Given a slice of entries, create a binary.
    pub fn write_all_to_bytes(src: &[Self]) -> Vec<u8> {
        let mut data = Vec::new();
        for entry in src {
            entry.write(&mut data).expect("should have no errors writing to vec");
        }
        data
    }
}
