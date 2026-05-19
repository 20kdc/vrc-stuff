mod progress;
mod render_svg;

use booklib::atlas_builder::*;
use booklib::docmodel::*;
use booklib::highlevel;
use booklib::progress::*;
use booklib::raster_helpers::*;
use booklib::rendered::*;
use lexopt::ValueExt;
use progress::ProgressImpl;
use render_svg::*;

const FULLCOLOUR_BLUE_DEFAULT: u8 = 8;
const RENDER_MUL_DEFAULT: f32 = 16.0;
const RENDER_MUL_IMG_DEFAULT: f32 = 16.0;
const RENDER_LIMIT_DEFAULT: u32 = 512;
const RENDER_LIMIT_IMG_DEFAULT: u32 = 2048;
const NEVER_SOLID_BELOW_DEFAULT: f32 = -16f32;
const SDF_DOWNSCALE_DEFAULT: usize = 4;
const SDF_BORDER_DEFAULT: u32 = 4;
const SDF_SMOOTH_DEFAULT: f32 = 8f32;
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
    // help options
    println!("");
    println!("HELP OPTIONS");
    println!(" --help: help");
    // output options
    println!("");
    println!("OUTPUT OPTIONS");
    println!(" -o OUTDIR: output directory, created if it doesn't exist");
    println!(" --web: Forces single 2k x 2k atlas w/ embedded data.");
    println!("        This is for downloading via VRCImageDownloader.");
    println!("        Implies --no-dae. Volumes are ignored.");
    println!("        A book.bytes file is written for testing purposes.");
    println!(" --no-dae: don't make DAE files");
    println!(" --no-fullcolour: shader is pure-SDF (no fullcolour image support)");
    println!(" --fullcolour-blue V: shader relies on some blue being present to");
    println!("  detect fullcolour black vs. SDF.");
    println!("  if 0, the shader is assumed to be standard 'alpha over' w/ no SDF");
    println!("  default is {}", FULLCOLOUR_BLUE_DEFAULT);
    println!("");
    println!("INPUT OPTIONS");
    // input options
    println!(" --volume NAME: separates into volumes that share atlases");
    println!(" --metadata-file FILE: merges a JSON object into metadata from a file");
    println!(" --metadata JSON: merges a JSON object into metadata from command line");
    println!("");
    println!("PER-FILE OPTIONS");
    println!(" These affect following files, not preceding ones (put them at the start).");
    println!(
        " --mupdf-w W: MuPDF reflow width, default {:?}",
        inputlib::LAYOUT_A5_W
    );
    println!(
        " --mupdf-h H: MuPDF reflow height, default {:?}",
        inputlib::LAYOUT_A5_H
    );
    println!(
        " --mupdf-em EM: MuPDF reflow EM, default {:?}",
        inputlib::LAYOUT_A5_EM
    );
    // renderer
    println!("");
    println!("RENDERER OPTIONS");
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
        " --img-render-mul VAL: for bitmaps, default {:?}",
        RENDER_MUL_IMG_DEFAULT
    );
    println!(
        " --img-render-limit VAL: for bitmaps. default {}",
        RENDER_LIMIT_IMG_DEFAULT
    );
    // post-filter
    println!("");
    println!("POST-FILTER OPTIONS");
    println!(" --invert: Inverts colours");
    // sdf
    println!("");
    println!("SDF OPTIONS");
    println!(" --sdf-everything: SDF things that don't look sane to SDF.");
    println!("  Use this if things that should be vectors aren't.");
    println!(" --never-solid-below VAL: Any object below this size in page units,");
    println!("  is never considered 'solid' (rectangle).");
    println!("  Use if 'l' is being converted and causing trouble.");
    println!("  If under 0, negated and a divisor of page size.");
    println!("  Default: {}", NEVER_SOLID_BELOW_DEFAULT);
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
    println!("");
    println!("ATLASING OPTIONS");
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
    println!("  if a single page requires more atlas space, this will be exceeded!");
    // debug
    println!("");
    println!("DEBUG OPTIONS");
    println!(" --debug-shapesearly: writes debug.dse.p*.s*.png");
    println!(" --debug-shapeslate: writes debug.s*.png / debug.s*.sdf.png");
    println!(" --debug-bigbox: runs all renders in page AABB to debug transform issues");
    println!(" --debug-noclip: disables renderer page bounds clipping");
    println!(" --debug-scaler-internal: downscales using internal box filter");
    std::process::exit(0);
}

fn parse_arg<
    V: std::str::FromStr<Err: Into<Box<dyn std::error::Error + Send + Sync + 'static>>>,
>(
    arg_parser: &mut lexopt::Parser,
    err: &str,
) -> V {
    let errstr = format!("--{} expects {}", err, std::any::type_name::<V>());
    arg_parser.value().expect(&errstr).parse().expect(&errstr)
}

enum InputNote {
    /// Volumes are converted into 'end volume' commands.
    EndVolume(String),
    Input(String, inputlib::InputOpts),
}

fn main() {
    // -- options --
    // output
    let mut outdir: Option<String> = None;
    let mut no_dae = false;
    let mut no_fullcolour = false;
    let mut fullcolour_blue: u8 = FULLCOLOUR_BLUE_DEFAULT;
    let mut web = false;
    // input
    let mut inopt = inputlib::InputOpts {
        mupdf_w: inputlib::LAYOUT_A5_W,
        mupdf_h: inputlib::LAYOUT_A5_H,
        mupdf_em: inputlib::LAYOUT_A5_EM,
    };
    // **ONLY** use in passing to shapeify_all!
    // Render coordinates are now per-shape.
    let mut cfg_render_mul: f32 = RENDER_MUL_DEFAULT;
    let mut cfg_render_mul_img: f32 = RENDER_MUL_IMG_DEFAULT;
    let mut render_limit: u32 = RENDER_LIMIT_DEFAULT;
    let mut render_limit_img: u32 = RENDER_LIMIT_IMG_DEFAULT;
    // post-filter
    let mut invert: bool = false;
    // sdf
    let mut sdf_everything: bool = false;
    let mut never_solid_below: f32 = NEVER_SOLID_BELOW_DEFAULT;
    let mut sdf_downscale: usize = SDF_DOWNSCALE_DEFAULT;
    let mut scaler: RasterScaler = RasterScaler::Skia;
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
    let mut debug_noclip = false;
    // files
    let mut current_volume: String = "book".to_string();
    let mut inputs: Vec<InputNote> = Vec::new();
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
                let vc = v.to_string();
                if v.eq("no-dae") {
                    no_dae = true;
                } else if v.eq("no-fullcolour") || v.eq("no-fullcolor") {
                    no_fullcolour = true;
                } else if v.eq("fullcolour-blue") || v.eq("fullcolor-blue") {
                    fullcolour_blue = parse_arg(&mut arg_parser, &vc);
                } else if v.eq("web") {
                    web = true;
                } else if v.eq("help") {
                    do_help();
                } else if v.eq("mupdf-w") {
                    inopt.mupdf_w = parse_arg(&mut arg_parser, &vc);
                } else if v.eq("mupdf-h") {
                    inopt.mupdf_h = parse_arg(&mut arg_parser, &vc);
                } else if v.eq("mupdf-em") {
                    inopt.mupdf_em = parse_arg(&mut arg_parser, &vc);
                } else if v.eq("volume") {
                    let vp: String = parse_arg(&mut arg_parser, &vc);
                    if inputs.is_empty() {
                        // We don't want to generate an empty volume at the start just for specifying the first volume name.
                        current_volume = vp;
                    } else {
                        inputs.push(InputNote::EndVolume(current_volume));
                        current_volume = vp;
                    }
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
                } else if v.eq("render-mul") {
                    cfg_render_mul = parse_arg(&mut arg_parser, &vc);
                } else if v.eq("render-limit") {
                    render_limit = parse_arg(&mut arg_parser, &vc);
                } else if v.eq("img-render-mul") {
                    cfg_render_mul_img = parse_arg(&mut arg_parser, &vc);
                } else if v.eq("img-render-limit") {
                    render_limit_img = parse_arg(&mut arg_parser, &vc);
                } else if v.eq("invert") {
                    invert = !invert;
                } else if v.eq("sdf-everything") {
                    sdf_everything = true;
                } else if v.eq("never-solid-below") {
                    never_solid_below = parse_arg(&mut arg_parser, &vc);
                } else if v.eq("sdf-downscale") {
                    sdf_downscale = parse_arg(&mut arg_parser, &vc);
                } else if v.eq("sdf-border") {
                    sdf_border = parse_arg(&mut arg_parser, &vc);
                } else if v.eq("sdf-smooth") {
                    sdf_smooth = parse_arg(&mut arg_parser, &vc);
                } else if v.eq("atlas-perfchop") {
                    atlas_perfchop = parse_arg(&mut arg_parser, &vc);
                } else if v.eq("atlas-min-size") {
                    atlas_min_size = parse_arg(&mut arg_parser, &vc);
                } else if v.eq("atlas-max-size") {
                    atlas_max_size = parse_arg(&mut arg_parser, &vc);
                } else if v.eq("debug-shapesearly") {
                    debug_dump_shapes_early = true;
                } else if v.eq("debug-shapeslate") {
                    debug_dump_shapes_late = true;
                } else if v.eq("debug-bigbox") {
                    debug_bigbox = true;
                } else if v.eq("debug-noclip") {
                    debug_noclip = true;
                } else if v.eq("debug-scaler-internal") {
                    scaler = RasterScaler::Internal;
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
                            inputs.push(InputNote::Input(candidate, inopt.clone()));
                        }
                        page_no += 1;
                    }
                } else {
                    inputs.push(InputNote::Input(x.to_string(), inopt.clone()));
                }
            }
        }
    }
    // -- options locked in --
    // end current volume
    inputs.push(InputNote::EndVolume(current_volume));
    let outdir = outdir.expect("outdir REQUIRED");
    _ = std::fs::create_dir_all(&outdir);
    let render_opts = RenderOpts {
        outdir: outdir.clone(),
        no_fullcolour,
        fullcolour_blue,
        sdf_border,
        render_limit,
        render_limit_img,
        cfg_render_mul,
        cfg_render_mul_img,
        sdf_everything,
        never_solid_below,
        debug_dse: debug_dump_shapes_early,
        debug_bigbox,
        debug_noclip,
    };
    // -- Page separation and SVG rendering --
    ProgressImpl.stage("page separation");
    let mut input_pages: Vec<(String, String)> = Vec::new();
    // Volume data. This is used in the .bytes emitter.
    let mut volumes: Vec<(String, usize, usize)> = Vec::new();
    {
        let mut volume_start: usize = 0;
        for file in inputs {
            match file {
                InputNote::EndVolume(volume) => {
                    volumes.push((volume, volume_start, input_pages.len()));
                    volume_start = input_pages.len();
                }
                InputNote::Input(name, inopts) => {
                    let doc =
                        inputlib::read(&name, &inopts).expect(&format!("{} should read", name));
                    for page in doc.enumerate() {
                        input_pages
                            .push((format!("{}:{}", name, page.0), page.1));
                    }
                }
            }
        }
    }
    // -- SVG rendering --
    ProgressImpl.stage("rendering");
    let mut shape_lookup = DBShapeLookup::default();
    let mut pages: Vec<DBPage> = Vec::new();
    let svg_opts = usvg::Options {
        font_family: "Liberation Serif".to_string(),
        ..Default::default()
    };
    let mut max_sprite_count: usize = 0;
    let mut max_sprite_count_page: usize = 0;
    for (page_idx, (svgn, svgd)) in input_pages.iter().enumerate() {
        ProgressImpl.status(&format!(
            " {:<24} : ({:>3}%), total_shapes={:>6}",
            svgn,
            progress::percentage(page_idx, input_pages.len()),
            shape_lookup.shapes.len()
        ));
        let res = usvg::Tree::from_str(&svgd, &svg_opts)
            .expect(&format!("svg {} should have parsed", svgn));
        // render and insert sprites
        let mut rendered: DBRenderedPage = render_svg(&res, page_idx, &render_opts);
        if rendered.sprites.len() > max_sprite_count {
            max_sprite_count = rendered.sprites.len();
            max_sprite_count_page = page_idx;
        }
        // post-filter
        // TODO: This should really be handled sometime during rendering instead.
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
            progress::percentage(page_idx, input_pages.len()),
            shape_lookup.shapes.len()
        ));
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
            scaler,
            sdf_smooth,
        },
        &ProgressImpl,
    );

    if web {
        ProgressImpl.stage("atlasing...");
        let res = highlevel::atlas_web(metadata_override, &sdf_shapes, &pages, &ProgressImpl)
            .expect("web should succeed");
        std::fs::write(&format!("{}/atlas.0.png", outdir), res.0).unwrap();
        std::fs::write(&format!("{}/book.bytes", outdir), res.1).unwrap();
    } else {
        // -- Atlasing --
        ProgressImpl.stage("atlasing...");
        let (mut atlas_builders, pages_atlased) = highlevel::atlas_pages(
            highlevel::AtlasPagesInput {
                sdf_shapes: &sdf_shapes,
                pages: &pages,
                atlas_min_size,
                atlas_max_size,
                atlas_perfchop,
            },
            &ProgressImpl,
        );

        let mut total_pixels: u64 = 0;
        ProgressImpl.stage("drawing atlases...");
        for (atlas_id, atlas_builder) in atlas_builders.iter().enumerate() {
            let atlas_pix = atlas_builder.render(&sdf_shapes);
            total_pixels += (atlas_pix.size().0 * atlas_pix.size().1) as u64;
            std::fs::write(
                &format!("{}/atlas.{}.png", outdir, atlas_id),
                raster_png(&atlas_pix),
            )
            .unwrap();
        }

        ProgressImpl.stage("emit...");
        // initialize atlased book
        let mut book_atlased = DBBook {
            metadata: metadata_override,
            atlases: atlas_builders.drain(..).map(|v| v.complete()).collect(),
            pages: vec![],
        };
        let mut emitted_len = 0;
        for (volume_name, volume_start, volume_end) in volumes {
            book_atlased.pages.clear();
            book_atlased.pages.extend(
                pages_atlased[volume_start..volume_end]
                    .iter()
                    .map(|v| v.clone()),
            );

            let emitted = book_atlased.emit();
            emitted_len += emitted.len();
            std::fs::write(&format!("{}/{}.bytes", outdir, volume_name), emitted).unwrap();

            if !no_dae {
                for i in 0..book_atlased.pages.len() {
                    std::fs::write(
                        &format!("{}/{}.{}.dae", outdir, volume_name, i),
                        booklib::collada::collada_write(&[book_atlased.page_dae(i)]),
                    )
                    .unwrap();
                    ProgressImpl.status(&format!(
                        " {:<24} DAE ({:>3}%)",
                        volume_name,
                        progress::percentage(i, book_atlased.pages.len())
                    ));
                }
            }
        }

        ProgressImpl.alert("book completed");
        println!("atlases: {}", book_atlased.atlases.len());
        println!("pages: {}", pages_atlased.len());
        println!(
            "max tris on one page: {} (pg. {})",
            (max_sprite_count * 2),
            max_sprite_count_page + 1
        );
        println!("texture VRAM: {:?}mb", (total_pixels as f32) / 1000000.0);
        println!(".bytes (RAM): {:?}mb", (emitted_len as f32) / 1000000.0);
    }
}
