//! Represents the input library.

use std::collections::VecDeque;
use std::io::Read;

/// We run MuPDF as a separate process due to complex build and licensing issues.
/// Obviously, this does not insulate you from your obligations to MuPDF under the AGPL if running a hosted web service.
pub struct PageHopperMuPDF {
    process: std::process::Child,
    line_bytes: Vec<u8>,
    page_lines: Vec<String>,
    pages: VecDeque<String>,
}

impl Iterator for PageHopperMuPDF {
    type Item = String;
    fn next(&mut self) -> Option<String> {
        if let Some(v) = self.pages.pop_front() {
            return Some(v);
        }
        let mut chunk: [u8; 16384] = [0; 16384];
        loop {
            let tmp = self.process.stdout.as_mut().unwrap();
            let sz = tmp.read(&mut chunk).unwrap();
            if sz == 0 {
                break;
            }
            // separate chunk into lines
            for b in &chunk[0..sz] {
                self.line_bytes.push(*b);
                if *b == 10 {
                    // add line
                    let line = String::from_utf8_lossy(&self.line_bytes).into_owned();
                    let is_end_of_page = line.trim().eq_ignore_ascii_case("</svg>");
                    self.page_lines.push(line);
                    self.line_bytes.clear();
                    // if end of page, then we add the page to the pages hopper for retrieval (once we're not at risk of losing data)
                    // notably, we can gain multiple pages in a single chunk in theory, so it's important this doesn't disturb consumption
                    if is_end_of_page {
                        let mut total = String::new();
                        for v in self.page_lines.drain(..) {
                            total.push_str(&v);
                        }
                        self.pages.push_back(total);
                    }
                }
            }
            if let Some(v) = self.pages.pop_front() {
                return Some(v);
            }
        }
        None
    }
}

impl Drop for PageHopperMuPDF {
    fn drop(&mut self) {
        _ = self.process.wait();
    }
}

/// Input options.
#[derive(Clone, Debug)]
pub struct InputOpts {
    pub mutool: Option<String>,
    pub mudraw_opts: Vec<String>,
}

impl InputOpts {
    pub fn find_mutool(&self) -> String {
        if let Some(cmd) = &self.mutool {
            return cmd.clone();
        }
        if let Ok(par) = std::env::current_exe() {
            if let Some(parpar) = par.parent() {
                let opts = vec![
                    parpar.join("mutool"),
                    parpar.join("mupdf/mutool"),
                    parpar.join("mutool.exe"),
                    parpar.join("mupdf/mutool.exe"),
                ];
                for a in opts {
                    if a.exists() {
                        return a.to_string_lossy().into_owned();
                    }
                }
            }
        }
        "mutool".to_string()
    }
}

pub fn read_svg(path: &str, _opts: &InputOpts) -> Result<Box<dyn Iterator<Item = String>>, String> {
    let s = std::fs::read_to_string(path).map_err(|v| format!("read SVG {:?}", v))?;
    Ok(Box::new(Some(s).into_iter()))
}

/// Reads from a path.
/// Note that a path is used for SVG autodetection.
pub fn read(path: &str, opts: &InputOpts) -> Result<Box<dyn Iterator<Item = String>>, String> {
    if path.ends_with(".svg") {
        read_svg(path, opts)
    } else {
        let mutool = opts.find_mutool();
        let mut cmd = std::process::Command::new(&mutool);
        cmd.arg("draw");
        cmd.arg("-o");
        cmd.arg("-");
        cmd.arg("-F");
        cmd.arg("svg");
        for v in &opts.mudraw_opts {
            cmd.arg(v);
        }
        cmd.arg("--");
        cmd.arg(path);
        cmd.stdout(std::process::Stdio::piped());
        let child = cmd
            .spawn()
            .map_err(|v| format!("spawn mutool '{}': {:?}", mutool, v))?;
        Ok(Box::new(PageHopperMuPDF {
            process: child,
            line_bytes: vec![],
            page_lines: vec![],
            pages: VecDeque::new(),
        }))
    }
}
