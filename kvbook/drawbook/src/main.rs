mod progress;
mod render_svg;

use booklib::atlas_builder::*;
use booklib::docmodel::*;
use booklib::highlevel;
use booklib::progress::*;
use booklib::rendered::*;
use lexopt::ValueExt;
use progress::ProgressImpl;
use render_svg::*;

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
    // output options
    println!(" -o OUTDIR: output directory, created if it doesn't exist");
    println!(" --no-dae: don't make DAE files");
    //
    println!(" --help: help");
    // renderer
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
    // post-filter
    println!(" --invert: Inverts colours");
    // sdf
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
    // atlasing
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
    // metadata
    println!(" --metadata-file FILE: merges a JSON object into metadata from a file");
    println!(" --metadata JSON: merges a JSON object into metadata from command line");
    // debug
    println!(" --debug-shapesearly: writes debug.dse.p*.s*.png");
    println!(" --debug-shapeslate: writes debug.s*.png / debug.s*.sdf.png");
    println!(" --debug-bigbox: runs all renders in page AABB to debug transform issues");
    std::process::exit(0);
}

fn main() {
    // -- options --
    // output
    let mut outdir: Option<String> = None;
    let mut no_dae = false;
    // renderer
    let mut split_aggression = SplitAggression::Isolatable;
    // **ONLY** use in passing to shapeify_all!
    // Render coordinates are now per-shape.
    let mut cfg_render_mul: f32 = RENDER_MUL_DEFAULT;
    let mut render_limit: u32 = RENDER_LIMIT_DEFAULT;
    // post-filter
    let mut invert: bool = false;
    // sdf
    let mut sdf_downscale: u32 = SDF_DOWNSCALE_DEFAULT;
    // Border in SDF pixels.
    let mut sdf_border: u32 = SDF_BORDER_DEFAULT;
    let mut sdf_smooth: f32 = SDF_SMOOTH_DEFAULT;
    // atlasing
    let mut atlas_perfchop: usize = ATLAS_PERFCHOP_DEFAULT;
    let mut atlas_min_size: usize = ATLAS_MIN_SIZE_DEFAULT;
    let mut atlas_max_size: usize = ATLAS_MAX_SIZE_DEFAULT;
    // debug
    let mut debug_dump_shapes_early = false;
    let mut debug_dump_shapes_late = false;
    let mut debug_bigbox = false;
    // files
    let mut inputs: Vec<String> = Vec::new();
    // metadata
    let mut metadata_override = json::object::Object::new();
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
                } else if v.eq("help") {
                    do_help();
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
                } else if v.eq("invert") {
                    invert = !invert;
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
                } else if v.eq("metadata-file") {
                    let vp = arg_parser.value().expect("--metadata-file expects a path");
                    let ff = std::fs::read_to_string(&vp)
                        .expect(&format!("metadata-file {:?} unreadable", vp));
                    let val =
                        json::parse(&ff).expect(&format!("metadata-file {:?} not valid JSON", vp));
                    for kv in val.entries() {
                        metadata_override.insert(kv.0, kv.1.clone());
                    }
                } else if v.eq("metadata") {
                    let vp = arg_parser
                        .value()
                        .expect("--metadata expects JSON")
                        .to_string_lossy()
                        .to_string();
                    let val = json::parse(&vp).expect("metadata not valid JSON");
                    for kv in val.entries() {
                        metadata_override.insert(kv.0, kv.1.clone());
                    }
                } else if v.eq("debug-shapesearly") {
                    debug_dump_shapes_early = true;
                } else if v.eq("debug-shapeslate") {
                    debug_dump_shapes_late = true;
                } else if v.eq("debug-bigbox") {
                    debug_bigbox = true;
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
        debug_dse: debug_dump_shapes_early,
        debug_bigbox: debug_bigbox,
    };
    // -- rasterize --
    ProgressImpl.stage("rendering");
    let mut shape_lookup = DBShapeLookup::default();
    let mut pages: Vec<DBPage> = Vec::new();
    let svg_opts = usvg::Options {
        ..Default::default()
    };
    for (page_idx, svgn) in inputs.iter().enumerate() {
        if let Ok(svgd) = std::fs::read(svgn) {
            ProgressImpl.status(&format!(
                " {:<24} : ({:>3}%), total_shapes={:>6}",
                svgn,
                progress::percentage(page_idx, inputs.len()),
                shape_lookup.shapes.len()
            ));
            let res = usvg::Tree::from_data(&svgd, &svg_opts).expect("svg should have parsed");
            // render and insert sprites
            let mut rendered: DBRenderedPage =
                render_svg(&res, split_aggression, page_idx, &render_opts);
            // post-filter
            if invert {
                for sprite in &mut rendered.sprites {
                    sprite.colour = [
                        255 - sprite.colour[0],
                        255 - sprite.colour[1],
                        255 - sprite.colour[2],
                        sprite.colour[3],
                    ];
                }
            }
            // complete
            pages.push(shape_lookup.deduplicate(rendered));
            ProgressImpl.status(&format!(
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
    ProgressImpl.stage("SDF conversions...");
    // 'rectangle' entries are None.
    let sdf_shapes: Vec<AtlasableShape> = highlevel::gen_sdf_shapes(
        highlevel::GenSDFShapesInput {
            shape_lookup: &shape_lookup,
            outdir: &outdir,
            debug_dump_shapes_late,
            sdf_downscale,
            sdf_smooth,
        },
        &ProgressImpl,
    );

    // -- Atlasing --
    ProgressImpl.stage("atlasing...");
    let (mut atlas_builders, pages_atlased) = highlevel::atlas_pages(
        highlevel::AtlasPagesInput {
            sdf_shapes: &sdf_shapes,
            pages: &pages,
            shape_lookup: &shape_lookup,
            atlas_min_size,
            atlas_max_size,
            atlas_perfchop,
        },
        &ProgressImpl,
    );

    ProgressImpl.stage("drawing atlases...");
    for (atlas_id, atlas_builder) in atlas_builders.iter().enumerate() {
        let atlas_pix = atlas_builder.render(&sdf_shapes);
        _ = std::fs::write(
            &format!("{}/atlas.{}.png", outdir, atlas_id),
            atlas_pix.encode_png().unwrap(),
        );
    }

    ProgressImpl.stage("emit...");
    // initialize atlased book
    let book_atlased = DBBook {
        metadata: metadata_override,
        atlases: atlas_builders.drain(..).map(|v| v.complete()).collect(),
        pages: pages_atlased,
    };
    _ = std::fs::write(&format!("{}/book.bytes", outdir), book_atlased.emit());
    if !no_dae {
        for i in 0..book_atlased.pages.len() {
            _ = std::fs::write(
                &format!("{}/page.{}.dae", outdir, i),
                booklib::collada::collada_write(&[book_atlased.page_dae(i)]),
            );
            ProgressImpl.status(&format!(
                " DAE ({:>3}%)",
                progress::percentage(i, book_atlased.pages.len())
            ));
        }
    }

    println!();
}
