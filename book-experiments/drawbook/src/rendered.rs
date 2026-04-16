//! This module is responsible for converting rasterized geometry into monochrome bitmapped 'rendered sprites'.
//! It's then responsible for mapping those sprites into a global lookup table.

use crate::docmodel::{DBPage, DBSprite};
use crate::geom::{Raster, V2};
use std::collections::HashMap;
use std::hash::Hasher;
use std::sync::Arc;
use tiny_skia::{Pixmap, PremultipliedColorU8};

/// A 'rendered shape' is something we're planning to convert into a signed distance field.
#[derive(Clone, PartialEq, Eq)]
pub struct DBRenderedShape {
    hash: u64,
    is_solid: bool,
    data: Raster<bool>,
    render_mul_bitsu32: u32,
}
impl std::hash::Hash for DBRenderedShape {
    fn hash<H: std::hash::Hasher>(&self, state: &mut H) {
        state.write_u64(self.hash);
    }
}
impl DBRenderedShape {
    pub fn new(data: Raster<bool>, render_mul: f32) -> DBRenderedShape {
        let mut state = std::hash::DefaultHasher::new();
        for v in data.data() {
            state.write_u8(*v as u8);
        }
        DBRenderedShape {
            hash: state.finish(),
            is_solid: data.area_eq_usize(V2(0, 0), data.size(), true),
            data,
            render_mul_bitsu32: render_mul.to_bits(),
        }
    }
    pub fn size(&self) -> V2<usize> {
        self.data.size()
    }
    pub fn data(&self) -> &Raster<bool> {
        &self.data
    }
    pub fn render_mul(&self) -> f32 {
        f32::from_bits(self.render_mul_bitsu32)
    }
    pub fn is_solid(&self) -> bool {
        self.is_solid
    }
    pub fn to_pixmap(&self) -> Pixmap {
        let col_black = PremultipliedColorU8::from_rgba(0, 0, 0, 255).unwrap();
        let col_white = PremultipliedColorU8::from_rgba(255, 255, 255, 255).unwrap();
        let mut downscale_check_canvas =
            Pixmap::new(self.size().0 as u32, self.size().1 as u32).unwrap();
        let mut downscale_check_mut = downscale_check_canvas.as_mut();
        let downscale_check_pixels = downscale_check_mut.pixels_mut();
        for i in self.data.data().iter().enumerate() {
            downscale_check_pixels[i.0] = if *i.1 { col_white } else { col_black };
        }
        downscale_check_canvas
    }
}

/// This struct is the 1-bit bitmap form of a _sprite,_ the fundamental unit of rendering in this program.
#[derive(Clone)]
pub struct DBRenderedSprite {
    /// Offset in page units.
    pub page_offset: V2<f32>,
    pub shape: DBRenderedShape,
    pub colour: [u8; 3],
}

pub fn shapeify(
    src: Pixmap,
    page_offset: V2<f32>,
    render_mul: f32,
    border: u32,
) -> Option<DBRenderedSprite> {
    let mut data = Vec::with_capacity((src.width() as usize) * (src.height() as usize));
    let mut counts_count = 0f32;
    let mut avg_r = 0f32;
    let mut avg_g = 0f32;
    let mut avg_b = 0f32;
    for pix in src.pixels() {
        let counts = pix.alpha() >= 128;
        data.push(counts);
        if counts {
            avg_r += pix.demultiply().red() as f32;
            avg_g += pix.demultiply().green() as f32;
            avg_b += pix.demultiply().blue() as f32;
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
    let mut crop_me = Raster::new(data, V2(src.width() as usize, src.height() as usize));

    let border_us = border as usize;
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
        crop_ul.0 = crop_ul.0.max(border_us) - border_us;
        crop_ul.1 = crop_ul.1.max(border_us) - border_us;

        crop_br.0 = (crop_br.0 + border_us).min(crop_me.size().0);
        crop_br.1 = (crop_br.1 + border_us).min(crop_me.size().1);
    }

    Some(DBRenderedSprite {
        page_offset: page_offset + V2(crop_ul.0 as f32 / render_mul, crop_ul.1 as f32 / render_mul),
        shape: DBRenderedShape::new(
            crop_me.extract_i32(
                V2(crop_ul.0 as i32, crop_ul.1 as i32),
                crop_br - crop_ul,
                false,
            ),
            render_mul,
        ),
        colour: [cr, cg, cb],
    })
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
            let top_left_page = shapeify_res.page_offset;
            page.sprites.push(DBSprite {
                shape: sprite_idx,
                top_left: top_left_page,
                colour: shapeify_res.colour,
            });
        }
        page
    }
}
