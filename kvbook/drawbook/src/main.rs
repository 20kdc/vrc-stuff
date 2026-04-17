use tiny_skia::Pixmap;

mod atlas_builder;
mod collada;
mod docmodel;
mod geom;
mod geom_atlas;
mod progress;
mod render_svg;
mod rendered;
mod sdf;

use atlas_builder::*;
use docmodel::*;
use geom::*;
use geom_atlas::*;
use lexopt::ValueExt;
use rayon::prelude::*;
use render_svg::*;
use rendered::*;

const RENDER_MUL_DEFAULT: f32 = 16.0;
const RENDER_LIMIT_DEFAULT: u32 = 512;
const SDF_DOWNSCALE_DEFAULT: u32 = 4;
const SDF_BORDER_DEFAULT: u32 = 4;
const SDF_SMOOTH_DEFAULT: f32 = 0.05f32;
const ATLAS_PERFCHOP_DEFAULT: usize = 131072;
const ATLAS_MIN_SIZE_DEFAULT: usize = 8;
const ATLAS_MAX_SIZE_DEFAULT: usize = 1024;

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
    println!(
        " --atlas-min-size VAL: atlas min size, default {:?}",
        ATLAS_MIN_SIZE_DEFAULT
    );
    println!(
        " --atlas-max-size VAL: atlas max size, default {:?}",
        ATLAS_MAX_SIZE_DEFAULT
    );
    println!("  if a single page requires more atlas space, this will be exceeded!",);
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
    let mut atlas_min_size: usize = ATLAS_MIN_SIZE_DEFAULT;
    let mut atlas_max_size: usize = ATLAS_MAX_SIZE_DEFAULT;
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
                } else if v.eq("atlas-min-size") {
                    atlas_min_size = arg_parser
                        .value()
                        .expect("--atlas-min-size expects usize")
                        .parse()
                        .expect("--atlas-min-size expects usize");
                } else if v.eq("atlas-max-size") {
                    atlas_max_size = arg_parser
                        .value()
                        .expect("--atlas-max-size expects usize")
                        .parse()
                        .expect("--atlas-max-size expects usize");
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
    progress::stage("rendering");
    let mut shape_lookup = DBShapeLookup::default();
    let mut pages: Vec<DBPage> = Vec::new();
    let svg_opts = usvg::Options {
        ..Default::default()
    };
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
    // -- SDF --
    progress::stage("SDF conversions...");
    // 'rectangle' entries are None.
    let sdf_shapes: Vec<Option<Pixmap>> = shape_lookup
        .shapes
        .par_iter()
        .enumerate()
        .map(|(shape_id, shape)| {
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
            Some(res_scaled)
        })
        .collect();

    // -- Atlasing --
    let mut atlas_builders: Vec<AtlasBuilder> = Vec::new();
    let mut pages_atlased: Vec<(u8, DBPage)> = Vec::new();
    {
        progress::stage("atlasing...");
        let mut curr_atlas =
            AtlasBuilder::new(V2(atlas_min_size, atlas_min_size), sdf_shapes.len());
        let mut has_alerted = false;
        for (k, page) in pages.iter().enumerate() {
            // attempt 1
            let watermark_pre_page = curr_atlas.watermark();
            let (ok, mut tf_page) = curr_atlas.atlas_page(
                page,
                &shape_lookup,
                &sdf_shapes,
                // if k == 0 here, this is a fresh atlas
                if k == 0 { None } else { Some(atlas_max_size) },
                atlas_perfchop,
            );
            if !ok {
                curr_atlas.revert_to_watermark(watermark_pre_page);
                progress::alert(&format!("index {:>6}: new atlas", k));
                atlas_builders.push(curr_atlas);
                curr_atlas =
                    AtlasBuilder::new(V2(atlas_min_size, atlas_min_size), sdf_shapes.len());
                has_alerted = false;
                (_, tf_page) =
                    curr_atlas.atlas_page(page, &shape_lookup, &sdf_shapes, None, atlas_perfchop);
            }
            if (curr_atlas.planner.size.0 > atlas_max_size
                || curr_atlas.planner.size.0 > atlas_max_size)
                && !has_alerted
            {
                has_alerted = true;
                progress::alert(&format!(
                    "index {:>6}: fresh atlas had to be expanded beyond max size",
                    k
                ));
            }
            pages_atlased.push((atlas_builders.len() as u8, tf_page));
            progress::status(&format!(
                " {:>3}% atlas={:>2} atlas_size={:?} freelist={:>8}        ",
                progress::percentage(k + 1, pages.len()),
                atlas_builders.len(),
                curr_atlas.planner.size,
                curr_atlas.planner.free.len()
            ));
        }
        atlas_builders.push(curr_atlas);
    }

    progress::stage("drawing atlases...");
    for (atlas_id, atlas_builder) in atlas_builders.iter().enumerate() {
        let mut atlas_pix = Pixmap::new(
            atlas_builder.planner.size.0 as u32,
            atlas_builder.planner.size.1 as u32,
        )
        .unwrap();
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
        for (local_id, v) in atlas_builder.placements.iter().enumerate() {
            let global_id = atlas_builder.inv_shape_map[local_id];
            if let Some(sdf) = &sdf_shapes[global_id] {
                atlas_pix.draw_pixmap(
                    v.uv_tl.0 as i32,
                    v.uv_tl.1 as i32,
                    sdf.as_ref(),
                    &tiny_skia::PixmapPaint::default(),
                    tiny_skia::Transform::identity(),
                    None,
                );
            }
        }
        _ = std::fs::write(
            &format!("{}/atlas.{}.png", outdir, atlas_id),
            atlas_pix.encode_png().unwrap(),
        );
    }

    progress::stage("emit...");
    // initialize atlased book
    let book_atlased = DBBook {
        atlases: atlas_builders.drain(..).map(|v| v.complete()).collect(),
        pages: pages_atlased,
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
