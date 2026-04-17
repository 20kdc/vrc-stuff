use std::io::Write;
use tiny_skia::Pixmap;

mod collada;
mod docmodel;
mod geom;
mod geom_atlas;
mod progress;
mod render_svg;
mod rendered;
mod sdf;

use docmodel::*;
use geom::*;
use geom_atlas::*;
use lexopt::ValueExt;
use rayon::prelude::*;
use render_svg::*;
use rendered::*;

const RENDER_MUL_DEFAULT: f32 = 16.0;
const RENDER_LIMIT_DEFAULT: u32 = 1024;
const SDF_DOWNSCALE_DEFAULT: u32 = 4;
const SDF_BORDER_DEFAULT: u32 = 4;
const SDF_SMOOTH_DEFAULT: f32 = 0.05f32;
const ATLAS_PERFCHOP_DEFAULT: usize = 16384;

fn do_help() {
    println!("drawbook IN... -o OUTDIR");
    println!(
        "{}",
        " converts a set of SVGs (pages/{1.svg, 2.svg...}) to SDF sheets"
    );
    println!(" IN: may be a single SVG, or if this contains '%d', this is");
    println!("  considered to be a sequence starting from either 0 or 1.");
    println!(" -o OUTDIR: output directory, created if it doesn't exist");
    println!(" --no-dae: don't make DAE files");
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
    println!(
        " --atlas-perfchop VAL: atlas freelist limit before 'forgetting', default {:?}",
        ATLAS_PERFCHOP_DEFAULT
    );
    println!(" --debug-shapeslate: writes debug.s*.png / debug.s*.sdf.png");
    std::process::exit(0);
}

fn main() {
    // -- options --
    let mut no_dae = false;
    let mut split_aggression = SplitAggression::Isolatable;
    // **ONLY** use in passing to shapeify_all!
    // Render coordinates are now per-shape.
    let mut cfg_render_mul: f32 = RENDER_MUL_DEFAULT;
    let mut render_limit: u32 = RENDER_LIMIT_DEFAULT;
    let mut sdf_downscale: u32 = SDF_DOWNSCALE_DEFAULT;
    // Border in SDF pixels.
    let mut sdf_border: u32 = SDF_BORDER_DEFAULT;
    let mut sdf_smooth: f32 = SDF_SMOOTH_DEFAULT;
    let mut atlas_perfchop: usize = ATLAS_PERFCHOP_DEFAULT;
    let mut debug_dump_shapes_late = false;
    let mut inputs: Vec<String> = Vec::new();
    let mut outdir: Option<String> = None;
    // -- argparse --
    let mut arg_parser = lexopt::Parser::from_env();
    while let Some(arg) = arg_parser.next().expect("arg_parser") {
        match arg {
            lexopt::Arg::Short(v) => {
                if v.eq(&'o') {
                    outdir = Some(
                        arg_parser
                            .value()
                            .expect("-o should have parameter")
                            .to_string_lossy()
                            .to_string(),
                    );
                } else {
                    panic!("unknown short arg {}, try --help", v);
                }
            }
            lexopt::Arg::Long(v) => {
                if v.eq("no-dae") {
                    no_dae = true;
                } else if v.eq("split") {
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
                } else if v.eq("atlas-perfchop") {
                    atlas_perfchop = arg_parser
                        .value()
                        .expect("--atlas-perfchop expects usize")
                        .parse()
                        .expect("--atlas-perfchop expects usize");
                } else if v.eq("debug-shapeslate") {
                    debug_dump_shapes_late = true;
                } else if v.eq("help") {
                    do_help();
                } else {
                    panic!("unknown long arg {}, try --help", v);
                }
            }
            lexopt::Arg::Value(v) => {
                // It's easier to handle sequencing here.
                let x = v.to_string_lossy();
                if x.contains("%d") {
                    let mut page_no = 0;
                    loop {
                        let candidate = x.replace("%d", &page_no.to_string());
                        if !std::fs::exists(&candidate).is_ok_and(|v| v) {
                            // This rule allows the sequence to start from 0 or 1.
                            if page_no != 0 {
                                break;
                            }
                        } else {
                            inputs.push(candidate);
                        }
                        page_no += 1;
                    }
                } else {
                    inputs.push(x.to_string());
                }
            }
        }
    }
    // -- options locked in --
    let outdir = outdir.expect("outdir REQUIRED");
    _ = std::fs::create_dir_all(&outdir);
    let render_opts = RenderOpts {
        outdir: outdir.clone(),
        sdf_border,
        render_limit,
        cfg_render_mul,
        debug_dse: false,
        debug_bigbox: false,
    };
    // -- rasterize --
    let mut shape_lookup = DBShapeLookup::default();
    let mut pages: Vec<DBPage> = Vec::new();
    let svg_opts = usvg::Options {
        ..Default::default()
    };
    progress::stage("rendering");
    for (page_idx, svgn) in inputs.iter().enumerate() {
        if let Ok(svgd) = std::fs::read(svgn) {
            progress::status(&format!(
                " {:<24} : ({:>3}%), total_shapes={:>6}",
                svgn,
                progress::percentage(page_idx, inputs.len()),
                shape_lookup.shapes.len()
            ));
            let res = usvg::Tree::from_data(&svgd, &svg_opts).expect("svg should have parsed");
            // render and insert sprites
            let rendered: DBRenderedPage = render_svg(&res, split_aggression, &render_opts);
            pages.push(shape_lookup.deduplicate(rendered));
            progress::status(&format!(
                " {:<24} : ({:>3}%), total_shapes={:>6}",
                svgn,
                progress::percentage(page_idx, inputs.len()),
                shape_lookup.shapes.len()
            ));
        } else {
            break;
        }
    }
    // done with main conversion bulk
    progress::stage("SDF conversions...");
    _ = std::io::Write::flush(&mut std::io::stdout().lock());
    // 'rectangle' entries are not included.
    let mut sdf_shapes: Vec<(usize, Pixmap)> = shape_lookup
        .shapes
        .par_iter()
        .enumerate()
        .filter_map(|(shape_id, shape)| {
            if debug_dump_shapes_late {
                let downscale_check_canvas = shape.to_pixmap();
                _ = std::fs::write(
                    format!("{}/debug.s{}.png", outdir, shape_id),
                    downscale_check_canvas.encode_png().unwrap(),
                );
            }

            if shape.is_solid() {
                return None;
            }

            let shape_sdf = sdf::shape_to_sdf(shape);

            // This is 'magic' that needs to act in concert with the shader.
            // It's important that it's scaled according to the 'total multiplier' so that it's spatially consistent.
            let step_sdf_mul = (shape.render_mul() as f32) / (sdf_downscale as f32);
            let step: f32 = 1f32 / (sdf_smooth * step_sdf_mul);

            let res = sdf::sdf_to_pixmap(&shape_sdf, (step as i32).max(1));
            if debug_dump_shapes_late {
                _ = std::fs::write(
                    format!("{}/debug.s{}.sdf.png", outdir, shape_id),
                    res.encode_png().unwrap(),
                );
            }
            let res_scaled = sdf::scale_pixmap(
                &res,
                sdf::downscale_size(&res, sdf_downscale),
                tiny_skia::FilterQuality::Bicubic,
            );
            progress::status(&format!(" last={:>6}", shape_id));
            Some((shape_id, res_scaled))
        })
        .collect();

    // 'pre-atlasing': we need these structs ready for when we actually do sort-and-place, since it can happen in a different order to 'encounter order'
    let mut shapes_atlased: Vec<DBAtlasedShape> = Vec::new();
    for shape in &shape_lookup.shapes {
        shapes_atlased.push(DBAtlasedShape {
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

    progress::stage("atlas planning...");
    let mut atlas: AtlasPage = AtlasPage::new(V2(8, 8));
    atlas.delete_under = V2(4, 4);
    atlas.mark(Rect {
        tl: V2(0, 0),
        br: V2(5, 5),
    });
    // actually plan the atlas
    for (k, v) in sdf_shapes.iter().enumerate() {
        loop {
            let pt = atlas.place(V2(v.1.width() as usize + 2, v.1.height() as usize + 2));
            if let Some(pt) = pt {
                if atlas.free.len() >= atlas_perfchop {
                    progress::alert("--atlas-perfchop freelist limit reached");
                    atlas.perf_chop();
                }
                shapes_atlased[v.0].uv_tl = V2(pt.0 as f32, pt.1 as f32) + V2(1f32, 1f32);
                shapes_atlased[v.0].uv_br = V2(
                    (pt.0 + v.1.width() as usize) as f32,
                    (pt.1 + v.1.height() as usize) as f32,
                ) + V2(1f32, 1f32);
                break;
            }
            atlas.enlarge();
            assert!(
                atlas.size.0 < 65536 && atlas.size.1 < 65536,
                "atlas size out of range"
            );
        }
        progress::status(&format!(
            " {:>3}% atlas_size={:?} shape_area={:>8} freelist={:>8}        ",
            progress::percentage(k + 1, sdf_shapes.len()),
            atlas.size,
            v.1.width() * v.1.height(),
            atlas.free.len()
        ));
        _ = std::io::stdout().flush();
    }

    progress::stage("drawing atlases...");
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
        progress::status(&format!(" last={:>6}", v.0));
    }
    _ = std::fs::write(
        &format!("{}/atlas.0.png", outdir),
        atlas_pix.encode_png().unwrap(),
    );

    progress::stage("emit...");
    // initialize atlased book
    let book_atlased = DBBook {
        atlases: vec![DBAtlas {
            size: V2(atlas.size.0 as u16, atlas.size.1 as u16),
            shapes: shapes_atlased,
        }],
        pages: pages.drain(..).map(|v| (0u8, v)).collect(),
    };
    _ = std::fs::write(&format!("{}/book.bytes", outdir), book_atlased.emit());
    if !no_dae {
        for i in 0..book_atlased.pages.len() {
            _ = std::fs::write(
                &format!("{}/page.{}.dae", outdir, i),
                collada::collada_write(&[book_atlased.page_dae(i)]),
            );
            progress::status(&format!(
                " DAE ({:>3}%)",
                progress::percentage(i, book_atlased.pages.len())
            ));
        }
    }

    println!();
}
