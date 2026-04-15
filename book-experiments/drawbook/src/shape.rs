use crate::geom::{Raster, V2};
use rayon::prelude::*;
use std::hash::Hasher;
use tiny_skia::{Pixmap, PremultipliedColorU8};

/// A 'shape' is something we're planning to convert into a signed distance field.
/// Shapes exist in an abstract 0-1 space.
/// This is to allow for a future level of 'acceptable loss' in comparison or downscaling.
#[derive(Clone, PartialEq, Eq)]
pub struct DBShape {
    hash: u64,
    is_solid: bool,
    data: Raster<bool>,
}
impl std::hash::Hash for DBShape {
    fn hash<H: std::hash::Hasher>(&self, state: &mut H) {
        state.write_u64(self.hash);
    }
}
impl DBShape {
    pub fn new(data: Raster<bool>) -> DBShape {
        let mut state = std::hash::DefaultHasher::new();
        for v in data.data() {
            state.write_u8(*v as u8);
        }
        DBShape {
            hash: state.finish(),
            is_solid: data.area_eq_usize(V2(0, 0), data.size(), true),
            data,
        }
    }
    pub fn size(&self) -> V2<usize> {
        self.data.size()
    }
    pub fn data(&self) -> &Raster<bool> {
        &self.data
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

/// 'shapeify' result
pub struct ShapeifyRes {
    /// Offset in render units.
    pub render_offset: V2<f32>,
    pub shape: DBShape,
    pub colour: [u8; 3],
}

pub fn shapeify(src: Pixmap, render_offset: V2<f32>, border: u32) -> Option<ShapeifyRes> {
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

    Some(ShapeifyRes {
        render_offset: render_offset + V2(crop_ul.0 as f32, crop_ul.1 as f32),
        shape: DBShape::new(crop_me.extract_i32(
            V2(crop_ul.0 as i32, crop_ul.1 as i32),
            crop_br - crop_ul,
            false,
        )),
        colour: [cr, cg, cb],
    })
}

/// Split aggression controls how 'aggressive' we are in terms of chopping up the render.
/// You almost always want to be on 'Isolatable' aggression, but sometimes 'Nasty' may be required (with implied rendering caveats).
#[derive(Clone, Copy, PartialEq, Eq, PartialOrd, Ord)]
pub enum SplitAggression {
    PageShape,
    RootChildren,
    Isolatable,
    Nasty,
}

impl SplitAggression {
    pub fn run(
        &self,
        src: &usvg::Group,
        tf: usvg::Transform,
    ) -> Vec<(usvg::Transform, usvg::Node)> {
        let (descend, deeper) = match self {
            Self::PageShape => (false, false),
            Self::RootChildren => (true, false),
            Self::Isolatable => (!src.should_isolate(), true),
            Self::Nasty => (true, true),
        };
        if !descend {
            vec![(tf, usvg::Node::Group(Box::new(src.clone())))]
        } else {
            // since we're now descending into children, we have to include the parent transform
            // see [resvg::render::render_group]
            let my_transform = tf.pre_concat(src.transform());
            let mut res = Vec::new();
            for v in src.children() {
                if deeper {
                    if let usvg::Node::Group(g) = v {
                        res.extend(self.run(g, my_transform));
                    } else {
                        res.push((my_transform, v.clone()));
                    }
                } else {
                    res.push((tf, v.clone()));
                }
            }
            res
        }
    }
}

/// Shapeify the contents of an SVG.
pub fn shapeify_all(
    tree: &usvg::Tree,
    split_aggression: SplitAggression,
    render_border: u32,
    render_mul: f32,
) -> Vec<ShapeifyRes> {
    // split into unprocessed sprites
    let sprites: Vec<(usvg::Transform, usvg::Node)> =
        split_aggression.run(tree.root(), usvg::Transform::identity());
    // render and insert sprites
    sprites
        .par_iter()
        .enumerate()
        .filter_map(|(j, (transform, sprite))| {
            if let Some(bbox) = sprite.abs_layer_bounding_box() {
                let render_border_doc = (render_border as f32) / render_mul;
                // bbox with border padding
                let adj_bbox = bbox
                    .to_rect()
                    .outset(render_border_doc, render_border_doc)
                    .unwrap();
                let mut temp_canvas = Pixmap::new(
                    (adj_bbox.width() * render_mul).ceil() as u32,
                    (adj_bbox.height() * render_mul).ceil() as u32,
                )
                .unwrap();
                let transform = transform
                    .post_scale(render_mul, render_mul)
                    .post_translate(render_border as f32, render_border as f32);
                if let Some(_) = resvg::render_node(&sprite, transform, &mut temp_canvas.as_mut()) {
                    if false {
                        _ = std::fs::write(
                            format!("debug/dse.s{}.png", j),
                            temp_canvas.encode_png().unwrap(),
                        );
                    }
                    // Notably, the hashing happens here, which amortizes the (sequential) shape_lookup.
                    shapeify(
                        temp_canvas,
                        V2(adj_bbox.left(), adj_bbox.top()) * V2(render_mul, render_mul),
                        render_border,
                    )
                } else {
                    None
                }
            } else {
                None
            }
        })
        .collect()
}
