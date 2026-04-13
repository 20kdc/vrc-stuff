use std::hash::Hasher;

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
    /// Sprite ID.
    pub sprite: usize,
    /// Top-left in 0-1 space.
    pub top_left: (f32, f32),
    /// Bottom-right in 0-1 space.
    pub bottom_right: (f32, f32),
    /// Colour.
    pub colour: [u8; 3],
}

/// A page of the book.
#[derive(Clone)]
pub struct DBPage {
    /// Page size in 'reference units'.
    /// Reference units are basically 'whatever came in' and are mainly useful for defining aspect ratio.
    /// All other position references operate in the following coordinate system:
    /// ```text
    /// 00-10
    /// |   |
    //  01-11
    /// ```
    pub size: (f32, f32),
    pub sprites: Vec<DBSprite>,
}

/// The book, at least before the SDF/output stage.
#[derive(Clone, Default)]
pub struct DBBook {
    /// Shapes, in bitmap form.
    /// These become SDFs (implicitly: haven't yet)
    pub shapes: Vec<DBShape>,
    pub pages: Vec<DBPage>,
}
