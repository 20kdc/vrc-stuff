/// This represents the various methods that drawbook can use to write its output files.
/// 'Pile' is a really terrible archive format.
/// It was introduced because APPARENTLY Godot 3.x's process reading hits the poolvector crash if you put too much into it.
/// So instead we're working around the Windows BS by just making one file rather than 100s.
pub enum Fileout {
    Dir(String),
    Pile(Vec<(String, Vec<u8>)>, String),
}

impl Fileout {
    pub fn begin(&mut self) {
        match self {
            Self::Dir(outdir) => {
                _ = std::fs::create_dir_all(outdir);
            }
            Self::Pile(_files, _outfile) => {}
        }
    }
    pub fn write(&mut self, filename: &str, data: Vec<u8>) {
        match self {
            Self::Dir(outdir) => {
                std::fs::write(&format!("{}/{}", outdir, filename), data).unwrap();
            }
            Self::Pile(files, _outfile) => {
                files.push((filename.to_string(), data));
            }
        }
    }
    pub fn debug_path(&self) -> Option<String> {
        match self {
            Self::Dir(outdir) => Some(outdir.clone()),
            Self::Pile(_files, _outfile) => None,
        }
    }
    pub fn finish(self) {
        match self {
            Self::Dir(_) => {
                // already done
            }
            Self::Pile(mut files, outfile) => {
                // bad metadata format:
                // [
                //  ["filename", 1234]
                // ]
                let mut array = json::Array::new();
                for v in &files {
                    array.push(json::array![v.0.clone(), v.1.len()]);
                }
                let mut contents: Vec<u8> = Vec::new();
                _ = json::JsonValue::Array(array).write(&mut contents);
                contents.push(0);
                for v in files.drain(..) {
                    contents.extend_from_slice(&v.1);
                }
                std::fs::write(outfile, contents).unwrap();
            }
        }
    }
}
