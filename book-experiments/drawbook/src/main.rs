use std::collections::HashMap;
use tiny_skia::{Pixmap, PremultipliedColorU8};

mod docmodel;
mod sdf;

use docmodel::*;

fn shapeify(src: Pixmap) -> (DBShape, [u8; 3]) {
    let mut data = Vec::with_capacity((src.width() as usize) * (src.height() as usize));
    let part = 1f32 / ((src.width() as f32) * (src.height() as f32));
    let mut avg_r = 0f32;
    let mut avg_g = 0f32;
    let mut avg_b = 0f32;
    for pix in src.pixels() {
        let counts = pix.alpha() >= 128;
        data.push(counts);
        if counts {
            avg_r += pix.demultiply().red() as f32 * part;
            avg_g += pix.demultiply().green() as f32 * part;
            avg_b += pix.demultiply().blue() as f32 * part;
        }
    }
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
    let render_mul: f32 = 4.0;
    let sdf_downscale: u32 = 4;
    let mut page_no = 0;
    let mut sprite_lookup: HashMap<DBShape, usize> = HashMap::new();
    let mut doc = DBBook::default();
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
                size: (res.size().width(), res.size().height()),
                sprites: Vec::new(),
            };
            // render and insert sprites
            for (j, sprite) in sprites.into_iter().enumerate() {
                if let Some(bbox) = sprite.abs_layer_bounding_box() {
                    let mut temp_canvas = Pixmap::new(
                        (bbox.width() * render_mul).ceil() as u32,
                        (bbox.height() * render_mul).ceil() as u32,
                    )
                    .unwrap();
                    let transform = usvg::Transform::identity()
                        .pre_translate(0.0f32, 0.0f32)
                        .pre_scale(render_mul, render_mul);
                    if let Some(_) =
                        resvg::render_node(&sprite, transform, &mut temp_canvas.as_mut())
                    {
                        if debug_dump_sprites_early {
                            _ = std::fs::write(
                                format!("debug/p{}.s{}.png", page_no, j),
                                temp_canvas.encode_png().unwrap(),
                            );
                        }
                        let results = shapeify(temp_canvas);
                        let sprite_idx = if let Some(sprite_idx) = sprite_lookup.get(&results.0) {
                            *sprite_idx
                        } else {
                            let res = doc.shapes.len();
                            doc.shapes.push(results.0.clone());
                            sprite_lookup.insert(results.0, res);
                            res
                        };
                        page.sprites.push(DBSprite {
                            sprite: sprite_idx,
                            top_left: (bbox.left() / page.size.0, bbox.top() / page.size.1),
                            bottom_right: (bbox.right() / page.size.0, bbox.bottom() / page.size.1),
                            colour: results.1,
                        });
                    }
                }
            }
            doc.pages.push(page);
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
        for (shape_id, shape) in doc.shapes.iter().enumerate() {
            if debug_dump_shapes_late {
                let mut temp_canvas =
                    Pixmap::new(shape.size().0 as u32, shape.size().1 as u32).unwrap();
                for i in shape.data().iter().enumerate() {
                    temp_canvas.as_mut().pixels_mut()[i.0] =
                        if *i.1 { col_white } else { col_black };
                }
                _ = std::fs::write(
                    format!("debug/s{}.png", shape_id),
                    temp_canvas.encode_png().unwrap(),
                );
            }
            let res = sdf::shape_to_sdf(&shape.border(4), 16);
            let res_size = (res.width() / sdf_downscale)
                .max(res.height() / sdf_downscale)
                .max(1)
                .next_power_of_two();
            let res_sdf = sdf::scale_pixmap(res, res_size, res_size);
            if debug_dump_shapes_late {
                _ = std::fs::write(
                    format!("debug/s{}.sdf.png", shape_id),
                    res_sdf.encode_png().unwrap(),
                );
            }
        }
    }
    // TODO: do writeout?
}
