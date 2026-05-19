//! This module is responsible for converting rasterized geometry into monochrome bitmapped 'rendered sprites'.
//! It's then responsible for mapping those sprites into a global lookup table.

use crate::docmodel::{DBPage, DBSprite};
use crate::geom::{Raster, V2};
use std::collections::HashMap;
use std::hash::Hasher;
use std::sync::Arc;
use tiny_skia::{ColorU8, Pixmap, PremultipliedColorU8};

#[derive(Clone, PartialEq, Eq)]
pub enum DBRenderedShapeData {
    Bitmap(Raster<bool>),
    Fullcolour(Raster<[u8; 4]>),
}

/// A 'rendered shape' is a comparable copy of the _data_ of a sprite.
/// This is the structure which gets deduplicated.
/// It can either be a bitmap for future conversion to SDF, or it can be a fullcolour pixmap.
/// If it's a fullcolour pixmap, it needs to be included in the atlas 'nearly as-is'.
/// (Fullcolour pixmaps may still be downscaled.)
#[derive(Clone, PartialEq, Eq)]
pub struct DBRenderedShape {
    hash: u64,
    is_solid: bool,
    data: DBRenderedShapeData,
    render_mul_bitsu32: u32,
}
impl std::hash::Hash for DBRenderedShape {
    fn hash<H: std::hash::Hasher>(&self, state: &mut H) {
        state.write_u64(self.hash);
    }
}
impl DBRenderedShape {
    pub fn new(data: DBRenderedShapeData, render_mul: f32) -> DBRenderedShape {
        let mut state = std::hash::DefaultHasher::new();
        match &data {
            DBRenderedShapeData::Bitmap(data) => {
                for v in data.data() {
                    state.write_u8(*v as u8);
                }
            }
            DBRenderedShapeData::Fullcolour(data) => {
                for v in data.data() {
                    state.write(v);
                }
            }
        }
        let is_solid = match &data {
            DBRenderedShapeData::Bitmap(data) => data.area_eq_usize(V2(0, 0), data.size(), true),
            DBRenderedShapeData::Fullcolour(_) => false,
        };
        DBRenderedShape {
            // Notably, the hashing happens here.
            // This means that it happens during the (parallel) rendering stage, rather than the (sequential) matching stage.
            hash: state.finish(),
            is_solid,
            data,
            render_mul_bitsu32: render_mul.to_bits(),
        }
    }
    pub fn size(&self) -> V2<usize> {
        match &self.data {
            DBRenderedShapeData::Bitmap(data) => data.size(),
            DBRenderedShapeData::Fullcolour(data) => data.size(),
        }
    }
    pub fn data(&self) -> &DBRenderedShapeData {
        &self.data
    }
    pub fn render_mul(&self) -> f32 {
        f32::from_bits(self.render_mul_bitsu32)
    }
    pub fn is_solid(&self) -> bool {
        self.is_solid
    }
    pub fn to_pixmap(&self) -> Pixmap {
        match &self.data {
            DBRenderedShapeData::Bitmap(data) => {
                let col_black = PremultipliedColorU8::from_rgba(0, 0, 0, 255).unwrap();
                let col_white = PremultipliedColorU8::from_rgba(255, 255, 255, 255).unwrap();
                let mut downscale_check_canvas =
                    Pixmap::new(self.size().0 as u32, self.size().1 as u32).unwrap();
                let mut downscale_check_mut = downscale_check_canvas.as_mut();
                let downscale_check_pixels = downscale_check_mut.pixels_mut();
                for i in data.data().iter().enumerate() {
                    downscale_check_pixels[i.0] = if *i.1 { col_white } else { col_black };
                }
                downscale_check_canvas
            }
            DBRenderedShapeData::Fullcolour(data) => {
                let mut downscale_check_canvas =
                    Pixmap::new(self.size().0 as u32, self.size().1 as u32).unwrap();
                let mut downscale_check_mut = downscale_check_canvas.as_mut();
                let downscale_check_pixels = downscale_check_mut.pixels_mut();
                for i in data.data().iter().enumerate() {
                    let c = ColorU8::from_rgba(i.1[0], i.1[1], i.1[2], i.1[3]);
                    downscale_check_pixels[i.0] = c.premultiply();
                }
                downscale_check_canvas
            }
        }
    }
}

pub type DBRenderedSprite = DBSprite<DBRenderedShape>;

/// Creates a new rendered sprite.
/// Handles auto-cropping and dead sprite elimination (returns None)
pub fn dbrenderedsprite_new(
    crop_me: &Raster<bool>,
    colour: [u8; 4],
    top_left: V2<f32>,
    render_mul: f32,
    border: usize,
) -> Option<DBRenderedSprite> {
    let (mut crop_ul, mut crop_br) = crop_me.find_crop_rectangle(false);

    if crop_ul.0 >= crop_br.0 || crop_ul.1 >= crop_br.1 {
        // Empty optimization
        return None;
    }

    if !crop_me.area_eq_usize(crop_ul, crop_br, true) {
        // Make sure to leave at least border_us pixels...
        // **unless** it's a solid rectangle.
        // If it's a solid rectangle, we want that to be plainly obvious down the line, so we allow these borders to be cropped off.
        // This allows the SDF generator to be aware that it can, in fact, not generate an SDF at all.
        crop_ul.0 = crop_ul.0.max(border) - border;
        crop_ul.1 = crop_ul.1.max(border) - border;

        crop_br.0 = (crop_br.0 + border).min(crop_me.size().0);
        crop_br.1 = (crop_br.1 + border).min(crop_me.size().1);
    }

    Some(DBRenderedSprite {
        top_left: top_left + V2(crop_ul.0 as f32 / render_mul, crop_ul.1 as f32 / render_mul),
        shape: DBRenderedShape::new(
            DBRenderedShapeData::Bitmap(crop_me.extract_i32(
                V2(crop_ul.0 as i32, crop_ul.1 as i32),
                crop_br - crop_ul,
                false,
            )),
            render_mul,
        ),
        colour,
    })
}

/// Creates a new rendered sprite.
/// Handles auto-cropping and dead sprite elimination (returns None)
pub fn dbrenderedsprite_new_fullcolour(
    crop_me: &Raster<[u8; 4]>,
    top_left: V2<f32>,
    render_mul: f32,
) -> Option<DBRenderedSprite> {
    let (crop_ul, crop_br) = crop_me.find_crop_rectangle([0, 0, 0, 0]);

    if crop_ul.0 >= crop_br.0 || crop_ul.1 >= crop_br.1 {
        // Empty optimization
        return None;
    }

    Some(DBRenderedSprite {
        top_left: top_left + V2(crop_ul.0 as f32 / render_mul, crop_ul.1 as f32 / render_mul),
        shape: DBRenderedShape::new(
            DBRenderedShapeData::Fullcolour(crop_me.extract_i32(
                V2(crop_ul.0 as i32, crop_ul.1 as i32),
                crop_br - crop_ul,
                [0, 0, 0, 0],
            )),
            render_mul,
        ),
        colour: [255, 255, 255, 255],
    })
}

#[derive(Clone, Copy, PartialEq, Eq, PartialOrd, Ord, Debug)]
pub enum ShapifyStrategy {
    AlphaClippedColourAverage,
    /// Experimental strategy for greyscale images.
    BWPrinting,
    /// Fullcolour (relies on magic shader technique)
    Fullcolour(u8),
}

/// Core noise function.
fn noise(i: u32) -> u32 {
    let mut val: u32 = 0x811c9dc5;
    for x in 0..4 {
        let b = (i >> ((x & 3) * 8)) & 0xFF;
        val ^= b;
        val = val.wrapping_mul(0x1000193);
    }
    val
}

static NOISE_TBL: std::sync::OnceLock<[u8; 0x10000]> = std::sync::OnceLock::new();

fn noise_table() -> &'static [u8; 0x10000] {
    NOISE_TBL.get_or_init(|| {
        let mut res = [0u8; 0x10000];
        for x in 0..256 {
            for y in 0..256 {
                res[x + (y * 0x100)] = noise((x as u32) | ((y as u32) << 8)) as u8;
            }
        }
        res
    })
}

impl ShapifyStrategy {
    pub fn shapeify(
        &self,
        src: Pixmap,
        page_offset: V2<f32>,
        render_mul: f32,
        border: u32,
    ) -> Option<DBRenderedSprite> {
        match self {
            Self::AlphaClippedColourAverage => {
                let mut data = Vec::with_capacity((src.width() as usize) * (src.height() as usize));
                let mut counts_count = 0f32;
                let mut avg_r = 0f32;
                let mut avg_g = 0f32;
                let mut avg_b = 0f32;
                // We get the maximum alpha in a first pass.
                // This lets us determine a reasonably safe threshold to prevent lower-alpha pixels (i.e. from AA) from causing too much colour drift.
                let mut max_a: u8 = 0;
                for pix in src.pixels() {
                    max_a = pix.alpha().max(max_a);
                }
                let threshold = max_a >> 1;
                for pix in src.pixels() {
                    let counts = pix.alpha() > threshold;
                    data.push(counts);
                    if counts {
                        let pix_dm = pix.demultiply();
                        avg_r += pix_dm.red() as f32;
                        avg_g += pix_dm.green() as f32;
                        avg_b += pix_dm.blue() as f32;
                        counts_count += 1f32;
                    }
                }
                avg_r /= counts_count;
                avg_g /= counts_count;
                avg_b /= counts_count;
                let cr = avg_r.clamp(0f32, 255f32).round() as u8;
                let cg = avg_g.clamp(0f32, 255f32).round() as u8;
                let cb = avg_b.clamp(0f32, 255f32).round() as u8;
                // figure out culling
                let crop_me = Raster::new(data, V2(src.width() as usize, src.height() as usize));
                dbrenderedsprite_new(
                    &crop_me,
                    [cr, cg, cb, max_a],
                    page_offset,
                    render_mul,
                    border as usize,
                )
            }
            Self::BWPrinting => {
                let noise_table = noise_table();
                let mut data = Vec::with_capacity((src.width() as usize) * (src.height() as usize));
                for y in 0..src.height() {
                    for x in 0..src.width() {
                        let pix =
                            src.pixels()[(x as usize) + ((y as usize) * (src.width() as usize))];
                        // remember, pix is premultiplied
                        let avg_pma =
                            (pix.red() as i32 + pix.green() as i32 + pix.blue() as i32) / 3;
                        // our background here is assumed to be white.
                        // therefore, we lower from white and then add back the average pre-multiplied colour.
                        let dst_mul = (255 - pix.alpha()) as i32;
                        let res = avg_pma + dst_mul;
                        // let res = (res * res) / 255;
                        //let xa1 = (x & 1) + ((y & 1) << 1);
                        //let threshold: i32 = [32, 96, 128, 64][xa1 as usize];
                        let threshold =
                            noise_table[(((y & 0xFF) << 8) + (x & 0xFF)) as usize] as i32;
                        data.push(res < threshold);
                    }
                }
                // figure out culling
                let crop_me = Raster::new(data, V2(src.width() as usize, src.height() as usize));
                dbrenderedsprite_new(
                    &crop_me,
                    [0, 0, 0, 255],
                    page_offset,
                    render_mul,
                    border as usize,
                )
            }
            Self::Fullcolour(blue) => {
                let mut data = Vec::with_capacity((src.width() as usize) * (src.height() as usize));
                for pix in src.pixels() {
                    let c = pix.demultiply();
                    data.push([c.red(), c.green(), c.blue().max(*blue), c.alpha()]);
                }
                let crop_me = Raster::new(data, V2(src.width() as usize, src.height() as usize));
                dbrenderedsprite_new_fullcolour(&crop_me, page_offset, render_mul)
            }
        }
    }
}

/// Represents a rendered page.
#[derive(Clone)]
pub struct DBRenderedPage {
    pub size: V2<f32>,
    pub sprites: Vec<DBRenderedSprite>,
}

/// Represents a shape lookup.
/// Deduplication of rendered shapes is the primary driving optimization which makes the whole endeavour possible and scalable to entire books.
/// Not only does it reduce atlas size to sensible values, but it also reduces the amount of SDF computations necessary.
#[derive(Clone, Default)]
pub struct DBShapeLookup {
    pub shape_lookup: HashMap<Arc<DBRenderedShape>, usize>,
    pub shapes: Vec<Arc<DBRenderedShape>>,
}

impl DBShapeLookup {
    /// Deduplicates an incoming [DBRenderedPage] into a [DBPage], adding any new shapes into the lookup.
    pub fn deduplicate(&mut self, mut rendered: DBRenderedPage) -> DBPage {
        let mut page = DBPage {
            size: rendered.size,
            sprites: Vec::new(),
        };
        for shapeify_res in rendered.sprites.drain(..) {
            let sprite_idx = if let Some(sprite_idx) = self.shape_lookup.get(&shapeify_res.shape) {
                *sprite_idx
            } else {
                let arc = Arc::new(shapeify_res.shape);
                let res = self.shapes.len();
                self.shapes.push(arc.clone());
                self.shape_lookup.insert(arc, res);
                res
            };
            page.sprites.push(DBSprite {
                shape: sprite_idx,
                top_left: shapeify_res.top_left,
                colour: shapeify_res.colour,
            });
        }
        page
    }
}
