use booklib::rendered::*;
use geomlib::*;
use rayon::prelude::*;
use tiny_skia::Pixmap;

pub struct RenderOpts {
    pub outdir: String,
    pub no_fullcolour: bool,
    pub fullcolour_blue: u8,
    pub sdf_border: u32,
    pub render_limit: u32,
    pub render_limit_img: u32,
    pub cfg_render_mul: f32,
    pub cfg_render_mul_img: f32,
    pub sdf_everything: bool,
    pub debug_dse: bool,
    pub debug_bigbox: bool,
    pub debug_noclip: bool,
}

/// Renderable object within the SVG.
/// Note that the node is actually a whole page due to how the new separator works.
struct SVGRenderable {
    page_size: (f32, f32),
    node: usvg::Node,
    content: svgseparator::ContentKind,
}

impl SVGRenderable {
    fn render_mul(&self, bbox_max_len: f32, opts: &RenderOpts) -> f32 {
        let render_limit = match self.content {
            svgseparator::ContentKind::Image => opts.render_limit_img,
            _ => opts.render_limit,
        };
        let render_mul = match self.content {
            svgseparator::ContentKind::Image => opts.cfg_render_mul_img,
            _ => opts.cfg_render_mul,
        };
        let render_mul_limit = (render_limit as f32) / bbox_max_len;
        render_mul.min(render_mul_limit)
    }
    pub fn render(&self, page_idx: usize, j: usize, opts: &RenderOpts) -> Vec<DBRenderedSprite> {
        let mut results: Vec<DBRenderedSprite> = Vec::new();
        if let Some(bbox) = self.node.abs_layer_bounding_box() {
            // Fit render into limit (ignoring border)
            let bbox_max_len = bbox.width().max(bbox.height());
            let render_mul = self.render_mul(bbox_max_len, opts);
            // Render multiplier is calculated, work with that from now on
            let render_border_doc = (opts.sdf_border as f32) / render_mul;
            // bbox with border padding
            let mut adj_bbox = bbox
                .to_rect()
                .outset(render_border_doc, render_border_doc)
                .unwrap();
            let page_box =
                usvg::Rect::from_xywh(0f32, 0f32, self.page_size.0, self.page_size.1).unwrap();
            if opts.debug_bigbox {
                adj_bbox = page_box;
            } else if !opts.debug_noclip {
                if let Some(b) = adj_bbox.intersect(&page_box) {
                    adj_bbox = b;
                } else {
                    // entirely clipped, reject
                    return results;
                }
            }
            let mut temp_canvas = Pixmap::new(
                (adj_bbox.width() * render_mul).ceil() as u32,
                (adj_bbox.height() * render_mul).ceil() as u32,
            )
            .unwrap();
            // Transforms the object perfectly into page-space.
            let bigbox_transform = usvg::Transform::from_scale(render_mul, render_mul);
            let transform = if opts.debug_bigbox {
                bigbox_transform
            } else {
                bigbox_transform
                    .post_translate(-adj_bbox.left() * render_mul, -adj_bbox.top() * render_mul)
            };
            if let Some(_) = resvg::render_node(
                &self.node,
                transform.pre_translate(bbox.x(), bbox.y()),
                &mut temp_canvas.as_mut(),
            ) {
                if opts.debug_dse {
                    _ = std::fs::write(
                        format!("{}/debug.dse.p{}.s{}.png", opts.outdir, page_idx, j),
                        temp_canvas.encode_png().unwrap(),
                    );
                }
                let sps = if opts.sdf_everything {
                    ShapifyStrategy::AlphaClippedColourAverage
                } else if opts.fullcolour_blue == 0 {
                    ShapifyStrategy::Fullcolour(opts.fullcolour_blue)
                } else {
                    match self.content {
                        svgseparator::ContentKind::Image => {
                            if opts.no_fullcolour {
                                ShapifyStrategy::BWPrinting
                            } else {
                                ShapifyStrategy::Fullcolour(opts.fullcolour_blue)
                            }
                        }
                        _ => ShapifyStrategy::AlphaClippedColourAverage,
                    }
                };
                results.extend(sps.shapeify(
                    temp_canvas,
                    V2(adj_bbox.left(), adj_bbox.top()),
                    render_mul,
                    opts.sdf_border,
                ));
            }
        }
        results
    }
}

/// Shapeify the contents of an SVG.
pub fn render_svg(tree: &usvg::Tree, page_idx: usize, render_opts: &RenderOpts) -> DBRenderedPage {
    // let usvg produce a clean tree for feeding to separator
    let usvg_src = tree.to_string(&usvg::WriteOptions {
        indent: usvg::Indent::None,
        ..usvg::WriteOptions::default()
    });
    // perform separation
    let mut sprites: Vec<(usvg::Tree, svgseparator::ContentKind)> = Vec::new();
    svgseparator::separator_main(&usvg_src, &mut |consider| {
        // We have to do parsing early because otherwise the memory usage will be killer.
        let tree = usvg::Tree::from_str(&consider.0, &usvg::Options::default()).unwrap();
        sprites.push((tree, consider.1));
    })
    .expect("separator should not throw errors");
    // render and insert sprites
    let rendered = sprites
        .par_iter()
        .enumerate()
        .map(|(j, (tree, kind))| {
            let renderable = SVGRenderable {
                node: usvg::Node::Group(Box::new(tree.root().clone())),
                page_size: (tree.size().width(), tree.size().height()),
                content: *kind,
            };
            renderable.render(page_idx, j, render_opts)
        })
        .flatten()
        .collect();
    DBRenderedPage {
        size: V2(tree.size().width(), tree.size().height()),
        sprites: rendered,
    }
}
