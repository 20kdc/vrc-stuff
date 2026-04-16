use crate::geom::V2;
use crate::rendered::*;
use rayon::prelude::*;
use tiny_skia::Pixmap;

/// Split aggression controls how 'aggressive' we are in terms of chopping up the render.
/// You almost always want to be on 'Isolatable' aggression, but sometimes 'Nasty' may be required (with implied rendering caveats).
#[derive(Clone, Copy, PartialEq, Eq, PartialOrd, Ord)]
pub enum SplitAggression {
    PageShape,
    RootChildren,
    Isolatable,
    Nasty,
}

impl SplitAggression {
    fn run(
        &self,
        page_size: (f32, f32),
        src: &usvg::Group,
        tf: usvg::Transform,
    ) -> Vec<SVGRenderable> {
        let (descend, deeper) = match self {
            Self::PageShape => (false, false),
            Self::RootChildren => (true, false),
            Self::Isolatable => (!src.should_isolate(), true),
            Self::Nasty => (true, true),
        };
        if !descend {
            vec![SVGRenderable {
                parent_transform: tf,
                node: usvg::Node::Group(Box::new(src.clone())),
                page_size,
            }]
        } else {
            // since we're now descending into children, we have to include the parent transform
            // see [resvg::render::render_group]
            let my_transform = tf.pre_concat(src.transform());
            let mut res = Vec::new();
            for v in src.children() {
                if deeper {
                    if let usvg::Node::Group(g) = v {
                        res.extend(self.run(page_size, g, my_transform));
                    } else {
                        res.push(SVGRenderable {
                            page_size,
                            parent_transform: my_transform,
                            node: v.clone(),
                        });
                    }
                } else {
                    res.push(SVGRenderable {
                        page_size,
                        parent_transform: my_transform,
                        node: v.clone(),
                    });
                }
            }
            res
        }
    }
}

pub struct RenderOpts {
    pub outdir: String,
    pub sdf_border: u32,
    pub render_limit: u32,
    pub cfg_render_mul: f32,
    pub debug_dse: bool,
    pub debug_bigbox: bool,
}

/// Renderable object within the SVG.
struct SVGRenderable {
    page_size: (f32, f32),
    parent_transform: usvg::Transform,
    node: usvg::Node,
}

impl SVGRenderable {
    pub fn render(&self, j: usize, opts: &RenderOpts) -> Vec<DBRenderedSprite> {
        let mut results: Vec<DBRenderedSprite> = Vec::new();
        if let Some(bbox) = self.node.abs_layer_bounding_box() {
            // Fit render into limit (ignoring border)
            let bbox_max_len = bbox.width().max(bbox.height());
            let render_mul_limit = (opts.render_limit as f32) / bbox_max_len;
            let render_mul = opts.cfg_render_mul.min(render_mul_limit);
            // Render multiplier is calculated, work with that from now on
            let render_border_doc = (opts.sdf_border as f32) / render_mul;
            // bbox with border padding
            let mut adj_bbox = bbox
                .to_rect()
                .outset(render_border_doc, render_border_doc)
                .unwrap();
            if opts.debug_bigbox {
                adj_bbox =
                    usvg::Rect::from_xywh(0f32, 0f32, self.page_size.0, self.page_size.1).unwrap();
            }
            let mut temp_canvas = Pixmap::new(
                (adj_bbox.width() * render_mul).ceil() as u32,
                (adj_bbox.height() * render_mul).ceil() as u32,
            )
            .unwrap();
            // Transforms the object perfectly into page-space.
            let bigbox_transform = usvg::Transform::from_scale(render_mul, render_mul)
                .pre_concat(self.parent_transform)
                .pre_translate(bbox.x(), bbox.y());
            let transform = if opts.debug_bigbox {
                bigbox_transform
            } else {
                bigbox_transform
                    .post_translate(-adj_bbox.left() * render_mul, -adj_bbox.top() * render_mul)
            };
            if let Some(_) = resvg::render_node(&self.node, transform, &mut temp_canvas.as_mut()) {
                if opts.debug_dse {
                    _ = std::fs::write(
                        format!("{}/debug.dse.s{}.png", opts.outdir, j),
                        temp_canvas.encode_png().unwrap(),
                    );
                }
                // Notably, the hashing happens here, which amortizes the (sequential) shape_lookup.
                results.extend(shapeify(
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
pub fn render_svg(
    tree: &usvg::Tree,
    split_aggression: SplitAggression,
    render_opts: &RenderOpts,
) -> DBRenderedPage {
    // let sdf_border: u32 = 0;
    // split into unprocessed sprites
    let sprites: Vec<SVGRenderable> = split_aggression.run(
        (tree.size().width(), tree.size().height()),
        tree.root(),
        usvg::Transform::identity(),
    );
    // render and insert sprites
    let rendered = sprites
        .par_iter()
        .enumerate()
        .map(|(j, renderable)| renderable.render(j, render_opts))
        .flatten()
        .collect();
    if render_opts.debug_dse {
        std::process::exit(0);
    }
    DBRenderedPage {
        size: V2(tree.size().width(), tree.size().height()),
        sprites: rendered,
    }
}
