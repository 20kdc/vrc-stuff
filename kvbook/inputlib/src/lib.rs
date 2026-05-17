//! Represents the input library.

pub trait PageHopper {
    /// Gets the page count.
    fn page_count(&self) -> usize;
    /// Gets a page as SVG text.
    fn page_to_svg(&self, page: usize) -> String;
}

pub struct PageHopperSVG(pub String);
pub struct PageHopperMuPDF(pub mupdf::Document);

impl PageHopper for PageHopperSVG {
    fn page_count(&self) -> usize {
        1
    }
    fn page_to_svg(&self, _page: usize) -> String {
        self.0.clone()
    }
}

impl PageHopper for PageHopperMuPDF {
    fn page_count(&self) -> usize {
        self.0.page_count().unwrap() as usize
    }
    fn page_to_svg(&self, page: usize) -> String {
        let page = self.0.load_page(page as i32).unwrap();
        page.to_svg(&mupdf::Matrix::IDENTITY).unwrap()
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

/// Reads from a path.
/// Note that a path is used for SVG autodetection.
pub fn read(path: &str, opts: &InputOpts) -> Result<Box<dyn PageHopper>, String> {
    if path.ends_with(".svg") {
        let s = std::fs::read_to_string(path).map_err(|v| format!("read SVG {:?}", v))?;
        Ok(Box::new(PageHopperSVG(s)))
    } else {
        let mut s = mupdf::Document::open(path).map_err(|v| format!("inputlib open {:?}", v))?;
        if s.is_reflowable()
            .map_err(|v| format!("inputlib is_reflowable {:?}", v))?
        {
            s.layout(opts.mupdf_w, opts.mupdf_h, opts.mupdf_em)
                .map_err(|v| format!("inputlib layout {:?}", v))?;
        }
        Ok(Box::new(PageHopperMuPDF(s)))
    }
}
