use kudonasm::*;
use kudonast::*;
use kudonodin::*;

fn help() -> ! {
    println!("kudonknife INFMT IN... OUTFMT OUT...");
    println!(" kudonknife converts Udon programs between different formats.");
    println!(" This includes acting as a KU2 assembler.");
    println!(" formats:");
    println!("  ku2 FILE (input only)");
    println!("  udonjson FILE");
    println!("  odinron FILE");
    println!("  odinbin FILE");
    println!("  uasm FILE (output only)");
    println!("  coredump FILE (input only)");
    std::process::exit(0)
}

enum Intermediate {
    /// Udon program.
    UdonProgram(kudonast::UdonProgram),
    /// Raw program (from coredumps etc.)
    UdonRawProgram(kudonast::UdonAnnotatedRawProgram),
}

impl Intermediate {
    fn into_program(self) -> Result<kudonast::UdonProgram, String> {
        match self {
            Self::UdonProgram(udonprogram) => Ok(udonprogram),
            Self::UdonRawProgram(res) => Ok(udonannotatedrawprogram_disassemble(&res)),
        }
    }
    fn into_raw_program(self) -> Result<kudonast::UdonAnnotatedRawProgram, String> {
        match self {
            Self::UdonProgram(udonprogram) => kudonast::UdonAnnotatedRawProgram::link(&udonprogram),
            Self::UdonRawProgram(res) => Ok(res),
        }
    }
}

fn get_filename(purpose: &str, args: &mut std::env::Args) -> Result<String, String> {
    if let Some(filename) = args.next() {
        Ok(filename)
    } else {
        return Err(format!("{}: missing filename", purpose));
    }
}

fn get_filetext(purpose: &str, args: &mut std::env::Args) -> Result<(String, String), String> {
    let ku2_filename = get_filename(purpose, args)?;
    Ok((
        std::fs::read_to_string(&ku2_filename)
            .map_err(|err| format!("{} {}: can't read: {:?}", purpose, ku2_filename, err))?,
        ku2_filename,
    ))
}
fn get_filebin(purpose: &str, args: &mut std::env::Args) -> Result<Vec<u8>, String> {
    let ku2_filename = get_filename(purpose, args)?;
    std::fs::read(&ku2_filename)
        .map_err(|err| format!("{} {}: can't read: {:?}", purpose, ku2_filename, err))
}

fn get_fileout(
    purpose: &str,
    args: &mut std::env::Args,
    outdat: impl AsRef<[u8]>,
) -> Result<(), String> {
    let ku2_filename = get_filename(purpose, args)?;
    std::fs::write(&ku2_filename, outdat)
        .map_err(|err| format!("{} {}: can't write: {:?}", purpose, ku2_filename, err))
}

#[derive(Clone, Copy, Debug)]
enum Format {
    KU2,
    UdonJson,
    OdinAST { binary: bool },
    UdonAssembly,
    Coredump,
}

impl Format {
    fn parse(a: &str) -> Result<Self, String> {
        if a.eq_ignore_ascii_case("ku2") {
            Ok(Self::KU2)
        } else if a.eq_ignore_ascii_case("udonjson") {
            Ok(Self::UdonJson)
        } else if a.eq_ignore_ascii_case("odinron") {
            Ok(Self::OdinAST { binary: false })
        } else if a.eq_ignore_ascii_case("odinbin") {
            Ok(Self::OdinAST { binary: true })
        } else if a.eq_ignore_ascii_case("uasm") {
            Ok(Self::UdonAssembly)
        } else if a.eq_ignore_ascii_case("coredump") {
            Ok(Self::Coredump)
        } else {
            Err(format!("unknown format {}", a))
        }
    }

    fn input(&self, args: &mut std::env::Args) -> Result<Intermediate, String> {
        match self {
            Self::KU2 => {
                let (ku2_text, ku2_filename) = get_filetext("ku2 input", args)?;
                let instrs = kudonasm_parse(&ku2_text).map_err(|err| format!("{:?}", err))?;
                let mut ku2_context = KU2Context::default();
                let mut program = UdonProgram::default();
                ku2_context.assemble_file(&mut program, &ku2_filename, &instrs)?;
                return Ok(Intermediate::UdonProgram(program));
            }
            Self::OdinAST { binary } => {
                let file: OdinASTFile = if *binary {
                    let odin_data = get_filebin("odinbin input", args)?;
                    let entries = OdinEntry::read_all_from_slice(&odin_data)
                        .map_err(|err| format!("{:?}", err))?;
                    kudonodin::OdinASTFile::from_entries(entries)
                } else {
                    let (odinast_text, _) = get_filetext("odinast input", args)?;
                    ron::from_str(&odinast_text).map_err(|err| format!("odinast: {:?}", err))?
                };
                let root_val = file
                    .get_root_value()
                    .ok_or_else(|| format!("odinbin has no root value"))?;
                let program: kudonast::UdonRawProgram =
                    OdinSTDeserializable::deserialize(&file.refs, root_val)?;
                return Ok(Intermediate::UdonRawProgram(
                    UdonAnnotatedRawProgram::from_unannotated(program),
                ));
            }
            Self::UdonJson => {
                // even though it doesn't work right now, have the code in place
                let (udonjson_text, _) = get_filetext("udonjson input", args)?;
                let udonjson_val =
                    json::parse(&udonjson_text).map_err(|err| format!("udonjson: {:?}", err))?;
                return Ok(Intermediate::UdonRawProgram(
                    UdonAnnotatedRawProgram::from_udonjson(&udonjson_val)?,
                ));
            }
            Self::UdonAssembly => {
                // we can't parse this
            }
            Self::Coredump => {
                let odin_data = get_filebin("coredump input", args)?;
                let core_dump: kudonast::UdonCoreDump =
                    OdinSTDeserializable::deserialize_bytes(&odin_data)?;
                // update coredump state
                // this allows the disassembly to act as a coherent read of most VM state
                // (which is the only reason you'd do this)
                let mut core_dump_program = core_dump.program;
                let mut core_dump_heap = core_dump.heap;
                if core_dump_program.heap.0.len() > core_dump_heap.0.len() {
                    for (k, v) in core_dump_heap.0.drain(..).enumerate() {
                        core_dump_program.heap.0[k] = v;
                    }
                } else {
                    core_dump_program.heap = core_dump_heap;
                }
                return Ok(Intermediate::UdonRawProgram(
                    UdonAnnotatedRawProgram::from_unannotated(core_dump_program),
                ));
            }
        }
        return Err(format!("format {:?} does not support input", self));
    }
    fn output(&self, args: &mut std::env::Args, src: Intermediate) -> Result<(), String> {
        match self {
            Self::KU2 => {
                return Err(format!("ku2 can't be output"));
            }
            Self::OdinAST { binary } => {
                let rawprogram = src.into_raw_program()?;

                let mut builder = OdinASTBuilder::default();
                let val = OdinSTSerializable::serialize(&rawprogram.program, &mut builder);
                builder.file.root.push(OdinASTEntry::uval(val));

                if *binary {
                    get_fileout(
                        "odinbin output",
                        args,
                        OdinEntry::write_all_to_bytes(&builder.file.to_entry_vec()),
                    )?;
                } else {
                    let pcfg = ron::ser::PrettyConfig::new().indentor("\t");
                    get_fileout(
                        "odinast output",
                        args,
                        ron::ser::to_string_pretty(&builder.file, pcfg)
                            .expect("should translate properly"),
                    )?;
                }
            }
            Self::UdonJson => {
                let udonjson = src.into_raw_program()?.to_udonjson()?;
                get_fileout("odinast output", args, udonjson.dump())?;
            }
            Self::UdonAssembly => {
                let program = src.into_program()?;
                let uasm_writer = UASMWriter::default();
                let issues = udonprogram_emit_uasm(&program, &uasm_writer);
                if let Err(err) = issues {
                    println!("{}", err);
                }
                get_fileout("odinast output", args, uasm_writer.to_string())?;
            }
            Self::Coredump => {
                return Err(format!("coredumps can't be output"));
            }
        }
        Ok(())
    }
}

fn main_inner() -> Result<(), String> {
    let mut args = std::env::args();
    _ = args.next();

    let infmt = if let Some(v) = args.next() {
        v
    } else {
        help();
    };

    let intermediate =
        { Format::parse(&infmt)?.input(&mut args) }.map_err(|v| format!("input: {}", v))?;

    let outfmt = if let Some(v) = args.next() {
        v
    } else {
        help();
    };

    { Format::parse(&outfmt)?.output(&mut args, intermediate) }
        .map_err(|v| format!("output: {}", v))?;

    if let Some(v) = args.next() {
        Err(format!("unexpected arg {}", v))
    } else {
        Ok(())
    }
}

fn main() {
    if let Err(err) = main_inner() {
        eprintln!("{}", err);
        std::process::exit(1);
    }
}
