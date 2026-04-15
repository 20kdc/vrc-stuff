use std::collections::HashMap;
use std::io::Write;
use std::sync::Arc;
use tiny_skia::Pixmap;

mod atlas;
mod collada;
mod docmodel;
mod geom;
mod sdf;
mod shapify;
mod svgshapes;

use atlas::*;
use docmodel::*;
use geom::*;
use lexopt::ValueExt;
use rayon::prelude::*;
use shapify::*;
use svgshapes::*;

const RENDER_MUL_DEFAULT: f32 = 16.0;
const RENDER_LIMIT_DEFAULT: u32 = 1024;
const SDF_DOWNSCALE_DEFAULT: u32 = 4;
const SDF_BORDER_DEFAULT: u32 = 4;
const SDF_SMOOTH_DEFAULT: f32 = 0.05f32;

fn do_help() {
    println!("drawbook");
    println!(
        "{}",
        " converts a set of SVGs (pages/{1.svg, 2.svg...}) to SDF sheets"
    );
    println!(" --help: help");
    println!(" --split page/roots/isolatable/nasty: SVG shape split mode");
    println!("  page: renders each page as one shape");
    println!("   (avoid unless direly needed)");
    println!("  roots: renders the root objects of each page as shapes");
    println!("   (this tends to result in groups of multiple characters)");
    println!("  isolatable: renders isolatable objects of each page as shapes");
    println!("   (the default)");
    println!("  nasty: breaks through all groups");
    println!("   (risky for compatibility)");
    println!(
        " --render-mul VAL: render multiplier, default {:?}",
        RENDER_MUL_DEFAULT
    );
    println!(
        " --render-limit VAL: render size limit, default {}",
        RENDER_LIMIT_DEFAULT
    );
    println!("  render-limit overrules render-mul, punishing large objects");
    println!("  this prevents near-endless SDF propagation");
    println!(
        " --sdf-downscale VAL: SDF downscale from render, default {}",
        SDF_DOWNSCALE_DEFAULT
    );
    println!("  The SDF generator here requires at least some downscaling.");
    println!("  A minimum of 4 is recommended, else curvature will be poor.");
    println!("  To increase quality, increase --render-mul instead.");
    println!(
        " --sdf-border VAL: SDF border, default {}",
        SDF_BORDER_DEFAULT
    );
    println!(
        " --sdf-smooth VAL: SDF smoothness ('magic', adjust w/ shader), default {:?}",
        SDF_SMOOTH_DEFAULT
    );
    println!(" --debug-shapeslate: writes debug/s*.png / debug/s*.sdf.png");
    std::process::exit(0);
}

fn main() {
    // -- options --
    let mut split_aggression = SplitAggression::Isolatable;
    // **ONLY** use in passing to shapeify_all!
    // Render coordinates are now per-shape.
    let mut cfg_render_mul: f32 = RENDER_MUL_DEFAULT;
    let mut render_limit: u32 = RENDER_LIMIT_DEFAULT;
    let mut sdf_downscale: u32 = SDF_DOWNSCALE_DEFAULT;
    let mut sdf_smooth: f32 = SDF_SMOOTH_DEFAULT;
    // Border in SDF pixels.
    let mut sdf_border: u32 = SDF_BORDER_DEFAULT;
    let mut debug_dump_shapes_late = false;
    // -- argparse --
    let mut arg_parser = lexopt::Parser::from_env();
    while let Some(arg) = arg_parser.next().expect("arg_parser") {
        match arg {
            lexopt::Arg::Short(v) => {
                panic!("unknown short arg {}, try --help", v);
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
                    cfg_render_mul = arg_parser
                        .value()
                        .expect("--render-mul expects float")
                        .parse()
                        .expect("--render-mul expects float");
                } else if v.eq("render-limit") {
                    render_limit = arg_parser
                        .value()
                        .expect("--render-limit expects u32")
                        .parse()
                        .expect("--render-limit expects u32");
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
                } else if v.eq("sdf-smooth") {
                    sdf_smooth = arg_parser
                        .value()
                        .expect("--sdf-smooth expects float")
                        .parse()
                        .expect("--sdf-smooth expects float");
                } else if v.eq("debug-shapeslate") {
                    debug_dump_shapes_late = true;
                } else if v.eq("help") {
                    do_help();
                } else {
                    panic!("unknown long arg {}, try --help", v);
                }
            }
            lexopt::Arg::Value(v) => {
                panic!("can't handle value {}, try --help", v.to_string_lossy());
            }
        }
    }
    // -- rasterize --
    let mut page_no = 1;
    let mut sprite_lookup: HashMap<Arc<DBShape>, usize> = HashMap::new();
    let mut shapes: Vec<Arc<DBShape>> = Vec::new();
    let mut pages: Vec<DBPage> = Vec::new();
    let svg_opts = usvg::Options {
        ..Default::default()
    };
    println!("rendering...");
    loop {
        let svgn = format!("pages/{}.svg", page_no);
        if let Ok(svgd) = std::fs::read(&svgn) {
            println!("{}", svgn);
            let res = usvg::Tree::from_data(&svgd, &svg_opts).expect("svg should have parsed");
            // render and insert sprites
            let rendered: Vec<ShapeifyRes> = shapeify_all(
                &res,
                split_aggression,
                sdf_border,
                render_limit,
                cfg_render_mul,
            );
            let mut page = DBPage {
                atlas: 0,
                size: V2(res.size().width(), res.size().height()),
                sprites: Vec::new(),
            };
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
                let top_left_page = shapeify_res.page_offset;
                page.sprites.push(DBSprite {
                    shape: sprite_idx,
                    top_left: top_left_page,
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
    print!("SDF conversions...");
    _ = std::io::Write::flush(&mut std::io::stdout().lock());
    // 'rectangle' entries are not included.
    let mut sdf_shapes: Vec<(usize, Pixmap)> = shapes
        .par_iter()
        .enumerate()
        .filter_map(|(shape_id, shape)| {
            if debug_dump_shapes_late {
                let downscale_check_canvas = shape.to_pixmap();
                _ = std::fs::write(
                    format!("debug/s{}.png", shape_id),
                    downscale_check_canvas.encode_png().unwrap(),
                );
            }

            if shape.is_solid() {
                return None;
            }

            let shape_sdf = sdf::shape_to_sdf(shape);

            // This is 'magic' that needs to act in concert with the shader.
            // It broadly represents smoothness, and needs to be smaller if the texture distance is smaller.
            let step_sdf_mul = (shape.render_mul() as f32) / (sdf_downscale as f32);
            let step: f32 = 1f32 / (sdf_smooth * step_sdf_mul);

            let res = sdf::sdf_to_pixmap(&shape_sdf, (step as i32).max(1));
            if debug_dump_shapes_late {
                _ = std::fs::write(
                    format!("debug/s{}.sdf.png", shape_id),
                    res.encode_png().unwrap(),
                );
            }
            let res_scaled = sdf::scale_pixmap(
                &res,
                sdf::downscale_size(&res, sdf_downscale),
                tiny_skia::FilterQuality::Bicubic,
            );
            let mut stdout_lock = std::io::stdout().lock();
            _ = write!(
                &mut stdout_lock,
                "\rSDF conversions... last={}          ",
                shape_id
            );
            Some((shape_id, res_scaled))
        })
        .collect();
    println!("");

    // 'pre-atlasing': we need these structs ready for when we actually do sort-and-place, since it can happen in a different order to 'encounter order'
    let mut shapes_atlased: Vec<DBShapeAtlased> = Vec::new();
    for shape in &shapes {
        shapes_atlased.push(DBShapeAtlased {
            // set to the 'rectangle' texture
            uv_tl: V2(2f32, 2f32),
            uv_br: V2(3f32, 3f32),
            // Convert from render units into reference units.
            size: V2(shape.size().0 as f32, shape.size().1 as f32)
                / V2(shape.render_mul(), shape.render_mul()),
        });
    }
    // determine encounter order, place descending
    sdf_shapes
        .sort_by(|v1, v2| (v2.1.width() * v2.1.height()).cmp(&(v1.1.width() * v1.1.height())));

    println!("atlas planning...");
    let mut atlas: AtlasPage = AtlasPage {
        size: V2(128, 128),
        rects: Vec::new(),
        points: Vec::new(),
    };
    // this area is reserved for the 'rectangle' texture
    atlas.rects.push(Rect {
        tl: V2(0, 0),
        br: V2(5, 5),
    });
    atlas.points.push((V2(5, 0), false));
    atlas.points.push((V2(0, 5), false));
    atlas.points.push((V2(5, 5), true));
    // actually plan the atlas
    for v in &sdf_shapes {
        loop {
            let pt = atlas.place(V2(v.1.width() as usize + 2, v.1.height() as usize + 2));
            if let Some(pt) = pt {
                shapes_atlased[v.0].uv_tl = V2(pt.0 as f32, pt.1 as f32) + V2(1f32, 1f32);
                shapes_atlased[v.0].uv_br = V2(
                    (pt.0 + v.1.width() as usize) as f32,
                    (pt.1 + v.1.height() as usize) as f32,
                ) + V2(1f32, 1f32);
                break;
            }
            atlas.enlarge();
        }
        atlas.clean_points();
    }

    println!("drawing atlases...");
    let mut atlas_pix = Pixmap::new(atlas.size.0 as u32, atlas.size.1 as u32).unwrap();
    atlas_pix.fill(tiny_skia::Color::BLACK);
    atlas_pix.fill_rect(
        tiny_skia::Rect::from_xywh(1f32, 1f32, 3f32, 3f32).unwrap(),
        &tiny_skia::Paint {
            shader: tiny_skia::Shader::SolidColor(tiny_skia::Color::WHITE),
            ..Default::default()
        },
        tiny_skia::Transform::identity(),
        None,
    );
    for v in sdf_shapes {
        atlas_pix.draw_pixmap(
            shapes_atlased[v.0].uv_tl.0 as i32,
            shapes_atlased[v.0].uv_tl.1 as i32,
            v.1.as_ref(),
            &tiny_skia::PixmapPaint::default(),
            tiny_skia::Transform::identity(),
            None,
        );
    }
    _ = std::fs::write("atlas0.png", atlas_pix.encode_png().unwrap());

    println!("emit...");
    // initialize atlased book
    let book_atlased = DBBook {
        atlases: vec![DBAtlas {
            size: V2(atlas.size.0 as u16, atlas.size.1 as u16),
            shapes: shapes_atlased,
        }],
        pages,
    };
    _ = std::fs::write("book.bin", book_atlased.emit());
    _ = std::fs::write("book.dae", book_atlased.emit_dae());
}
