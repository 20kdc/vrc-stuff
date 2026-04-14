use std::hash::Hasher;

use crate::geom::V2;

/// A 'shape' is something we're planning to convert into a signed distance field.
/// Shapes exist in an abstract 0-1 space.
/// This is to allow for a future level of 'acceptable loss' in comparison or downscaling.
#[derive(Clone, PartialEq, Eq, PartialOrd, Ord)]
pub struct DBShape {
    hash: u64,
    data: Vec<bool>,
    size: (usize, usize),
}
impl std::hash::Hash for DBShape {
    fn hash<H: std::hash::Hasher>(&self, state: &mut H) {
        state.write_u64(self.hash);
    }
}
impl DBShape {
    pub fn new(data: Vec<bool>, w: usize, h: usize) -> DBShape {
        let mut state = std::hash::DefaultHasher::new();
        for v in &data {
            state.write_u8(*v as u8);
        }
        DBShape {
            hash: state.finish(),
            data,
            size: (w, h),
        }
    }
    pub fn size(&self) -> (usize, usize) {
        self.size
    }
    pub fn w(&self) -> usize {
        self.size.0
    }
    pub fn data(&self) -> &[bool] {
        &self.data
    }
    pub fn row(&self, y: usize) -> &[bool] {
        let base = y * self.size.0;
        &self.data[base..(base + self.size.0)]
    }
    pub fn index(&self, x: usize, y: usize) -> usize {
        x + (y * self.size.0)
    }
    pub fn border(&self, amount: usize) -> DBShape {
        let mut res = Vec::new();
        let res_w = self.size.0 + (amount * 2);
        for _ in 0..amount {
            for _ in 0..res_w {
                res.push(false);
            }
        }
        for y in 0..self.size.1 {
            for _ in 0..amount {
                res.push(false);
            }
            res.extend_from_slice(self.row(y));
            for _ in 0..amount {
                res.push(false);
            }
        }
        for _ in 0..amount {
            for _ in 0..res_w {
                res.push(false);
            }
        }
        DBShape::new(res, res_w, self.size.1 + (amount * 2))
    }
}

/// A 'sprite' is a single renderable entity in the book.
#[derive(Clone)]
pub struct DBSprite {
    /// Shape ID.
    pub shape: usize,
    /// Sprite position. This is defined in a 0-1 space as so:
    /// ```text
    /// 00-10
    /// |   |
    //  01-11
    /// ```
    pub top_left: V2<f32>,
    /// Colour.
    pub colour: [u8; 3],
}

/// A page of the book.
#[derive(Clone)]
pub struct DBPage {
    /// Page size in 'reference units'.
    /// Reference units are basically 'whatever came in'.
    /// Importantly, shape sizes are defined relative to them.
    pub size: V2<f32>,
    pub sprites: Vec<DBSprite>,
}

#[derive(Clone)]
pub struct DBShapeAtlased {
    pub atlas: u8,
    pub uv_tl: V2<f32>,
    pub uv_br: V2<f32>,
    /// Size in reference units.
    pub size: V2<f32>,
}

/// Proper atlased book structure.
#[derive(Clone, Default)]
pub struct DBBook {
    pub shapes: Vec<DBShapeAtlased>,
    pub pages: Vec<DBPage>,
}

impl DBBook {
    // -1 to 1
    pub fn emit_qf(v: f32) -> [u8; 2] {
        (((v * 32767f32).round() as i32).clamp(-32768, 32767) as i16).to_le_bytes()
    }
    pub fn emit_qv2(v: V2<f32>) -> [u8; 4] {
        let a = Self::emit_qf(v.0);
        let b = Self::emit_qf(v.1);
        [a[0], a[1], b[0], b[1]]
    }
    // 0 to 1
    pub fn emit_uf(v: f32) -> [u8; 2] {
        (((v * 65535f32).round() as i32).clamp(0, 65535) as u16).to_le_bytes()
    }
    pub fn emit_uv2(v: V2<f32>) -> [u8; 4] {
        let a = Self::emit_uf(v.0);
        let b = Self::emit_uf(v.1);
        [a[0], a[1], b[0], b[1]]
    }
    /// Writes book contents to a blob.
    pub fn emit(&self) -> Vec<u8> {
        let mut lumps: Vec<Vec<u8>> = Vec::new();
        // build shapes lump
        let mut shapes_lump: Vec<u8> = Vec::new();
        for shape in &self.shapes {
            shapes_lump.extend_from_slice(&[shape.atlas]);
            shapes_lump.extend_from_slice(&Self::emit_uv2(shape.uv_tl));
            shapes_lump.extend_from_slice(&Self::emit_uv2(shape.uv_br));
            shapes_lump.extend_from_slice(&shape.size.0.to_le_bytes());
            shapes_lump.extend_from_slice(&shape.size.1.to_le_bytes());
        }
        lumps.push(shapes_lump);
        // build page lumps
        for page in &self.pages {
            let mut page_lump: Vec<u8> = Vec::new();
            page_lump.extend_from_slice(&page.size.0.to_le_bytes());
            page_lump.extend_from_slice(&page.size.1.to_le_bytes());
            for sprite in &page.sprites {
                page_lump.extend_from_slice(&(sprite.shape as u16).to_le_bytes());
                page_lump.extend_from_slice(&Self::emit_qv2(sprite.top_left));
                // colour to RGB565
                let r = sprite.colour[0] as u32;
                let g = sprite.colour[1] as u32;
                let b = sprite.colour[2] as u32;
                let rgb565 = ((r << 8) & 0xF800) | ((g << 3) & 0x07E0) | ((b >> 3) & 0x001F);
                page_lump.extend_from_slice(&(rgb565 as u16).to_le_bytes());
            }
            lumps.push(page_lump);
        }
        // write out header/lumps
        let mut out: Vec<u8> = Vec::new();
        out.extend_from_slice(&(lumps.len() as u32).to_le_bytes());
        let mut lpos = 4 + (lumps.len() * 8);
        for lump in &lumps {
            out.extend_from_slice(&(lpos as u32).to_le_bytes());
            out.extend_from_slice(&(lump.len() as u32).to_le_bytes());
            lpos += lump.len();
        }
        // write out lump data
        for lump in &lumps {
            out.extend_from_slice(lump);
        }
        out
    }
}
