use std::collections::HashMap;
use tiny_skia::{Pixmap, PremultipliedColorU8};

mod docmodel;
mod geom;
mod sdf;

use docmodel::*;
use geom::*;
use rayon::prelude::*;

type ShapeifyRes = (DBShape, [u8; 3]);

fn shapeify(src: Pixmap) -> ShapeifyRes {
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
    (
        DBShape::new(data, src.width() as usize, src.height() as usize),
        [cr, cg, cb],
    )
}

fn main() {
    let debug_single_sprite = false;
    let debug_dump_sprites_early = false;
    let debug_dump_shapes_late = true;
    let render_mul: f32 = 16.0;
    let sdf_downscale: u32 = 4;
    // Border in render pixels.
    // This should be pre-multiplied by sdf_downscale.
    let render_border: f32 = 4f32;
    let mut page_no = 0;
    let mut sprite_lookup: HashMap<DBShape, usize> = HashMap::new();
    // The size in reference units is stored here for reasonably easy consistency.
    // There's an implication here that two shapes with slightly different sizes and identical rasterizations will be mismatched.
    // 'Oh well'.
    let mut shapes: Vec<(DBShape, V2<f32>)> = Vec::new();
    let mut pages: Vec<DBPage> = Vec::new();
    let svg_opts = usvg::Options {
        ..Default::default()
    };
    println!("compiling...");
    loop {
        let svgn = format!("pages/{}.svg", page_no);
        if let Ok(svgd) = std::fs::read(&svgn) {
            println!("{}", svgn);
            let res = usvg::Tree::from_data(&svgd, &svg_opts).expect("svg should have parsed");
            // split into unprocessed sprites
            let sprites: Vec<usvg::Node> = if debug_single_sprite {
                vec![usvg::Node::Group(Box::new(res.root().clone()))]
            } else {
                res.root().children().iter().map(|v| v.clone()).collect()
            };
            let mut page = DBPage {
                size: V2(res.size().width(), res.size().height()),
                sprites: Vec::new(),
            };
            // render and insert sprites
            let rendered: Vec<(ShapeifyRes, tiny_skia::Rect)> = sprites
                .par_iter()
                .enumerate()
                .filter_map(|(j, sprite)| {
                    if let Some(bbox) = sprite.abs_layer_bounding_box() {
                        // bbox with border padding
                        let adj_bbox = bbox
                            .to_rect()
                            .outset(render_border / render_mul, render_border / render_mul)
                            .unwrap();
                        let mut temp_canvas = Pixmap::new(
                            (adj_bbox.width() * render_mul).ceil() as u32,
                            (adj_bbox.height() * render_mul).ceil() as u32,
                        )
                        .unwrap();
                        let transform = usvg::Transform::identity()
                            .post_scale(render_mul, render_mul)
                            .post_translate(render_border, render_border);
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
                            let results = shapeify(temp_canvas);
                            Some((results, adj_bbox))
                        } else {
                            None
                        }
                    } else {
                        None
                    }
                })
                .collect();
            for ((shape, colour), adj_bbox) in rendered {
                let sprite_idx = if let Some(sprite_idx) = sprite_lookup.get(&shape) {
                    *sprite_idx
                } else {
                    let res = shapes.len();
                    shapes.push((shape.clone(), V2(adj_bbox.width(), adj_bbox.height())));
                    sprite_lookup.insert(shape, res);
                    res
                };
                page.sprites.push(DBSprite {
                    shape: sprite_idx,
                    top_left: V2(adj_bbox.left(), adj_bbox.top()) / page.size,
                    colour,
                });
            }
            pages.push(page);
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
        for (shape_id, (shape, _)) in shapes.iter().enumerate() {
            let mut temp_canvas =
                Pixmap::new(shape.size().0 as u32, shape.size().1 as u32).unwrap();
            for i in shape.data().iter().enumerate() {
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
        .map(|(shape_id, (shape, _))| {
            let res = sdf::shape_to_sdf(shape, 16);
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
            size: shape.1,
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
