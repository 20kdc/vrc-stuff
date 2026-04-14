use std::collections::HashMap;
use std::sync::Arc;
use tiny_skia::{Pixmap, PremultipliedColorU8};

mod docmodel;
mod geom;
mod sdf;

use docmodel::*;
use geom::*;
use lexopt::ValueExt;
use rayon::prelude::*;

/// 'shapeify' result
struct ShapeifyRes {
    /// Offset in render units.
    render_offset: V2<f32>,
    shape: DBShape,
    colour: [u8; 3],
}

fn shapeify(src: Pixmap, render_offset: V2<f32>, border: u32) -> Option<ShapeifyRes> {
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
enum SplitAggression {
    PageShape,
    RootChildren,
    Isolatable,
    Nasty,
}

impl SplitAggression {
    fn run(&self, src: &usvg::Group, tf: usvg::Transform) -> Vec<(usvg::Transform, usvg::Node)> {
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

fn main() {
    // -- options --
    let mut split_aggression = SplitAggression::Isolatable;
    let mut render_mul: f32 = 16.0;
    let mut sdf_downscale: u32 = 4;
    // Border in SDF pixels.
    let mut sdf_border: u32 = 4;
    // -- argparse --
    let mut arg_parser = lexopt::Parser::from_env();
    while let Some(arg) = arg_parser.next().expect("arg_parser") {
        match arg {
            lexopt::Arg::Short(v) => {
                panic!("unknown short arg {}", v);
            }
            lexopt::Arg::Long(v) => {
                if v.eq("split") {
                    let msg = "--split expects one of: page, roots, isolatable, nasty";
                    let vp = arg_parser.value().expect(msg);
                    if vp.eq("page") {
                        split_aggression = SplitAggression::PageShape;
                    } else if vp.eq("roots") {
                        split_aggression = SplitAggression::RootChildren;
                    } else if vp.eq("isolatable") {
                        split_aggression = SplitAggression::Isolatable;
                    } else if vp.eq("nasty") {
                        split_aggression = SplitAggression::Nasty;
                    } else {
                        panic!("{}", msg);
                    }
                } else if v.eq("render-mul") {
                    render_mul = arg_parser
                        .value()
                        .expect("--render-mul expects float")
                        .parse()
                        .expect("--render-mul expects float");
                } else if v.eq("sdf-downscale") {
                    sdf_downscale = arg_parser
                        .value()
                        .expect("--sdf-downscale expects u32")
                        .parse()
                        .expect("--sdf-downscale expects u32");
                } else if v.eq("sdf-border") {
                    sdf_border = arg_parser
                        .value()
                        .expect("--sdf-border expects float")
                        .parse()
                        .expect("--sdf-border expects float");
                } else {
                    panic!("unknown long arg {}", v);
                }
            }
            lexopt::Arg::Value(v) => {
                panic!("can't handle value {}", v.to_string_lossy());
            }
        }
    }
    let debug_dump_sprites_early = false;
    let debug_dump_shapes_late = true;
    // -- rasterize --
    let mut page_no = 1;
    let mut sprite_lookup: HashMap<Arc<DBShape>, usize> = HashMap::new();
    let mut shapes: Vec<Arc<DBShape>> = Vec::new();
    let mut pages: Vec<DBPage> = Vec::new();
    let svg_opts = usvg::Options {
        ..Default::default()
    };
    println!("rendering...");
    // Border in render pixels.
    let render_border = sdf_border * sdf_downscale;
    loop {
        let svgn = format!("pages/{}.svg", page_no);
        if let Ok(svgd) = std::fs::read(&svgn) {
            println!("{}", svgn);
            let res = usvg::Tree::from_data(&svgd, &svg_opts).expect("svg should have parsed");
            // split into unprocessed sprites
            let sprites: Vec<(usvg::Transform, usvg::Node)> =
                split_aggression.run(res.root(), usvg::Transform::identity());
            let mut page = DBPage {
                size: V2(res.size().width(), res.size().height()),
                sprites: Vec::new(),
            };
            // render and insert sprites
            let rendered: Vec<ShapeifyRes> = sprites
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
                        if let Some(_) =
                            resvg::render_node(&sprite, transform, &mut temp_canvas.as_mut())
                        {
                            if debug_dump_sprites_early {
                                _ = std::fs::write(
                                    format!("debug/p{}.s{}.png", page_no, j),
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
                .collect();
            for shapeify_res in rendered {
                let sprite_idx = if let Some(sprite_idx) = sprite_lookup.get(&shapeify_res.shape) {
                    *sprite_idx
                } else {
                    let arc = Arc::new(shapeify_res.shape);
                    let res = shapes.len();
                    shapes.push(arc.clone());
                    sprite_lookup.insert(arc, res);
                    res
                };
                let top_left_page = shapeify_res.render_offset / V2(render_mul, render_mul);
                page.sprites.push(DBSprite {
                    shape: sprite_idx,
                    top_left: top_left_page / page.size,
                    colour: shapeify_res.colour,
                });
            }
            pages.push(page);
            println!(" {} shapes", sprite_lookup.len());
        } else {
            break;
        }
        page_no += 1;
    }
    // done with main conversion bulk
    if debug_dump_shapes_late {
        println!("dumping shapes...");
        let col_black = PremultipliedColorU8::from_rgba(0, 0, 0, 255).unwrap();
        let col_white = PremultipliedColorU8::from_rgba(255, 255, 255, 255).unwrap();
        for (shape_id, shape) in shapes.iter().enumerate() {
            let mut temp_canvas =
                Pixmap::new(shape.size().0 as u32, shape.size().1 as u32).unwrap();
            for i in shape.data().data().iter().enumerate() {
                temp_canvas.as_mut().pixels_mut()[i.0] = if *i.1 { col_white } else { col_black };
            }
            _ = std::fs::write(
                format!("debug/s{}.png", shape_id),
                temp_canvas.encode_png().unwrap(),
            );
        }
    }
    println!("SDF conversions...");
    let sdf_shapes: Vec<Pixmap> = shapes
        .par_iter()
        .enumerate()
        .map(|(shape_id, shape)| {
            // we want the constant 128 to be as high as feasible (so, 128)
            // this is because it's a major parameter in the current algorithm speed
            // and also helps with utilizing maximum dynamic range
            // the problem is, it's basically imitating 'spread', and thus needs to act in concert with the shader and texture size
            let res = sdf::shape_to_sdf(shape, (128 / sdf_downscale) as i32);
            let res_w = (res.width() / sdf_downscale).max(1);
            let res_h = (res.height() / sdf_downscale).max(1);
            let res_sdf = sdf::scale_pixmap(res, res_w, res_h);
            if debug_dump_shapes_late {
                _ = std::fs::write(
                    format!("debug/s{}.sdf.png", shape_id),
                    res_sdf.encode_png().unwrap(),
                );
            }
            res_sdf
        })
        .collect();
    // 'pre-atlasing': we need these structs ready for when we actually do sort-and-place, since it can happen in a different order to 'encounter order'
    let mut shapes_atlased: Vec<DBShapeAtlased> = Vec::new();
    for shape in &shapes {
        shapes_atlased.push(DBShapeAtlased {
            atlas: 0,
            uv_tl: V2(0f32, 0f32),
            uv_br: V2(0f32, 0f32),
            // Convert from render units into atlas units.
            size: V2(shape.size().0 as f32, shape.size().1 as f32) / V2(render_mul, render_mul),
        });
    }
    println!("atlasing...");
    // we WOULD do atlasing here, but to start getting quality checks early we're currently skipping that in favour of having the Godot test app read the dumps directly
    println!("emit...");
    // initialize atlased book
    let book_atlased = DBBook {
        shapes: shapes_atlased,
        pages: pages,
    };
    _ = std::fs::write("book.bin", book_atlased.emit());
}
