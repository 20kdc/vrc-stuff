use crate::atlas_builder::*;
use crate::docmodel::*;
use crate::geom::*;
use crate::progress::*;
use crate::rendered::*;
use crate::sdf::{downscale_size, scale_pixmap, sdf_to_pixmap, shape_to_sdf};
use rayon::prelude::*;

pub struct GenSDFShapesInput<'a> {
    pub shape_lookup: &'a DBShapeLookup,
    pub outdir: &'a str,
    pub debug_dump_shapes_late: bool,
    pub sdf_downscale: u32,
    pub sdf_smooth: f32,
}

/// Performs SDF conversion on the input shapes.
/// Rectangles are indicated as None.
pub fn gen_sdf_shapes(opt: GenSDFShapesInput, progress: &dyn Progress) -> Vec<AtlasableShape> {
    // 'rectangle' entries are None.
    opt.shape_lookup
        .shapes
        .par_iter()
        .enumerate()
        .map(|(shape_id, shape)| {
            if opt.debug_dump_shapes_late {
                let downscale_check_canvas = shape.to_pixmap();
                _ = std::fs::write(
                    format!("{}/debug.s{}.png", opt.outdir, shape_id),
                    downscale_check_canvas.encode_png().unwrap(),
                );
            }

            // Convert from render units into reference units.
            let size = V2(shape.size().0 as f32, shape.size().1 as f32)
                / V2(shape.render_mul(), shape.render_mul());

            if shape.is_solid() {
                return AtlasableShape::Rectangle { size };
            }

            let shape_sdf = shape_to_sdf(shape);

            // This is 'magic' that needs to act in concert with the shader.
            // It's important that it's scaled according to the 'total multiplier' so that it's spatially consistent.
            let step_sdf_mul = (shape.render_mul() as f32) / (opt.sdf_downscale as f32);
            let step: f32 = 1f32 / (opt.sdf_smooth * step_sdf_mul);

            let res = sdf_to_pixmap(&shape_sdf, (step as i32).max(1));
            if opt.debug_dump_shapes_late {
                _ = std::fs::write(
                    format!("{}/debug.s{}.sdf.png", opt.outdir, shape_id),
                    res.encode_png().unwrap(),
                );
            }
            let res_scaled = scale_pixmap(
                &res,
                downscale_size(&res, opt.sdf_downscale),
                tiny_skia::FilterQuality::Bicubic,
            );
            progress.status(&format!(" last={:>6}", shape_id));
            AtlasableShape::Pixmap {
                sdf: res_scaled,
                size,
            }
        })
        .collect()
}

pub struct AtlasPagesInput<'a> {
    pub sdf_shapes: &'a [AtlasableShape],
    pub pages: &'a [DBPage],
    pub atlas_min_size: usize,
    pub atlas_max_size: usize,
    pub atlas_perfchop: usize,
}

/// Builds atlases for pages.
pub fn atlas_pages(
    opt: AtlasPagesInput,
    progress: &dyn Progress,
) -> (Vec<AtlasBuilder>, Vec<(u8, DBPage)>) {
    let mut atlas_builders: Vec<AtlasBuilder> = Vec::new();
    let mut pages_atlased: Vec<(u8, DBPage)> = Vec::new();
    let mut curr_atlas = AtlasBuilder::new(
        V2(opt.atlas_min_size, opt.atlas_min_size),
        opt.sdf_shapes.len(),
    );
    let mut has_alerted = false;
    for (k, page) in opt.pages.iter().enumerate() {
        // attempt 1
        let watermark_pre_page = curr_atlas.watermark();
        let (ok, mut tf_page) = curr_atlas.atlas_page(
            page,
            opt.sdf_shapes,
            // if k == 0 here, this is a fresh atlas
            if k == 0 {
                None
            } else {
                Some(opt.atlas_max_size)
            },
            opt.atlas_perfchop,
            progress,
        );
        if !ok {
            curr_atlas.revert_to_watermark(watermark_pre_page);
            progress.alert(&format!("index {:>6}: new atlas", k));
            atlas_builders.push(curr_atlas);
            curr_atlas = AtlasBuilder::new(
                V2(opt.atlas_min_size, opt.atlas_min_size),
                opt.sdf_shapes.len(),
            );
            has_alerted = false;
            (_, tf_page) =
                curr_atlas.atlas_page(page, opt.sdf_shapes, None, opt.atlas_perfchop, progress);
        }
        if (curr_atlas.planner.size.0 > opt.atlas_max_size
            || curr_atlas.planner.size.0 > opt.atlas_max_size)
            && !has_alerted
        {
            has_alerted = true;
            progress.alert(&format!(
                "index {:>6}: fresh atlas had to be expanded beyond max size",
                k
            ));
        }
        pages_atlased.push((atlas_builders.len() as u8, tf_page));
        progress.status(&format!(
            " {:>3}% atlas={:>2} atlas_size={:?} freelist={:>8}        ",
            percentage(k + 1, opt.pages.len()),
            atlas_builders.len(),
            curr_atlas.planner.size,
            curr_atlas.planner.free.len()
        ));
    }
    atlas_builders.push(curr_atlas);
    (atlas_builders, pages_atlased)
}

/// 'Web format' exporter.
/// The format here is pretty simple:
/// We always create a 2048x2048-sized image, and no other files.
/// We store book data starting at the top-left. The bytes, in pixel order, are the exact bytes of the .bytes datafile.
/// The atlas we build is shifted down so that the bottom-most image nearly touches the bottom of the atlas (with a pixel for border).
pub fn atlas_web(
    _metadata: json::object::Object,
    _sdf_shapes: &[AtlasableShape],
    _pages: &[DBPage],
) -> Result<Vec<u8>, String> {
    Err("Not yet implemented".to_string())
}
