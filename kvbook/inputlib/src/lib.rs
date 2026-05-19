//! Represents the input library.

#[cfg(feature = "mupdf")]
pub struct PageHopperMuPDF(pub mupdf::Document, pub i32);

#[cfg(feature = "mupdf")]
impl Iterator for PageHopperMuPDF {
    type Item = String;
    fn next(&mut self) -> Option<Self::Item> {
        if self.1 >= self.0.page_count().unwrap() {
            None
        } else {
            let page = self.0.load_page(self.1).unwrap();
            self.1 += 1;
            Some(page.to_svg(&mupdf::Matrix::IDENTITY).unwrap())
        }
    }
}

pub const LAYOUT_A5_W: f32 = 420f32;
pub const LAYOUT_A5_H: f32 = 595f32;
pub const LAYOUT_A5_EM: f32 = 11f32;

/// Input options.
#[derive(Clone, Copy, Debug)]
pub struct InputOpts {
    pub mupdf_w: f32,
    pub mupdf_h: f32,
    pub mupdf_em: f32,
}

pub fn read_svg(path: &str, _opts: &InputOpts) -> Result<Box<dyn Iterator<Item = String>>, String> {
    let s = std::fs::read_to_string(path).map_err(|v| format!("read SVG {:?}", v))?;
    Ok(Box::new(Some(s).into_iter()))
}

#[cfg(not(feature = "mupdf"))]
pub fn read(path: &str, opts: &InputOpts) -> Result<Box<dyn Iterator<Item = String>>, String> {
    read_svg(path, opts)
}

/// Reads from a path.
/// Note that a path is used for SVG autodetection.
#[cfg(feature = "mupdf")]
pub fn read(path: &str, opts: &InputOpts) -> Result<Box<dyn Iterator<Item = String>>, String> {
    if path.ends_with(".svg") {
        read_svg(path, opts)
    } else {
        let mut s = mupdf::Document::open(path).map_err(|v| format!("inputlib open {:?}", v))?;
        if s.is_reflowable()
            .map_err(|v| format!("inputlib is_reflowable {:?}", v))?
        {
            s.layout(opts.mupdf_w, opts.mupdf_h, opts.mupdf_em)
                .map_err(|v| format!("inputlib layout {:?}", v))?;
        }
        Ok(Box::new(PageHopperMuPDF(s, 0)))
    }
}
