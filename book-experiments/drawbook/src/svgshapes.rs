use crate::geom::V2;
use crate::shapify::*;
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
    pub fn run(
        &self,
        src: &usvg::Group,
        tf: usvg::Transform,
    ) -> Vec<(usvg::Transform, usvg::Node)> {
        let (descend, deeper) = match self {
            Self::PageShape => (false, false),
            Self::RootChildren => (true, false),
            Self::Isolatable => (!src.should_isolate(), true),
            Self::Nasty => (true, true),
        };
        if !descend {
            vec![(tf, usvg::Node::Group(Box::new(src.clone())))]
        } else {
            // since we're now descending into children, we have to include the parent transform
            // see [resvg::render::render_group]
            let my_transform = tf.pre_concat(src.transform());
            let mut res = Vec::new();
            for v in src.children() {
                if deeper {
                    if let usvg::Node::Group(g) = v {
                        res.extend(self.run(g, my_transform));
                    } else {
                        res.push((my_transform, v.clone()));
                    }
                } else {
                    res.push((tf, v.clone()));
                }
            }
            res
        }
    }
}

/// Shapeify the contents of an SVG.
pub fn shapeify_all(
    tree: &usvg::Tree,
    split_aggression: SplitAggression,
    sdf_border: u32,
    render_limit: u32,
    cfg_render_mul: f32,
) -> Vec<ShapeifyRes> {
    let debug_dse = false;
    let debug_bigbox = false;
    // let sdf_border: u32 = 0;
    // split into unprocessed sprites
    let sprites: Vec<(usvg::Transform, usvg::Node)> =
        split_aggression.run(tree.root(), usvg::Transform::identity());
    // render and insert sprites
    let rendered = sprites
        .par_iter()
        .enumerate()
        .filter_map(|(j, (parent_transform, sprite))| {
            if let Some(bbox) = sprite.abs_layer_bounding_box() {
                // Fit render into limit (ignoring border)
                let bbox_max_len = bbox.width().max(bbox.height());
                let render_mul_limit = (render_limit as f32) / bbox_max_len;
                let render_mul = cfg_render_mul.min(render_mul_limit);
                // Render multiplier is calculated, work with that from now on
                let render_border_doc = (sdf_border as f32) / render_mul;
                // bbox with border padding
                let mut adj_bbox = bbox
                    .to_rect()
                    .outset(render_border_doc, render_border_doc)
                    .unwrap();
                if debug_bigbox {
                    adj_bbox = usvg::Rect::from_xywh(
                        0f32,
                        0f32,
                        tree.size().width(),
                        tree.size().height(),
                    )
                    .unwrap();
                }
                let mut temp_canvas = Pixmap::new(
                    (adj_bbox.width() * render_mul).ceil() as u32,
                    (adj_bbox.height() * render_mul).ceil() as u32,
                )
                .unwrap();
                // Transforms the object perfectly into page-space.
                let bigbox_transform = usvg::Transform::from_scale(render_mul, render_mul)
                    .pre_concat(*parent_transform)
                    .pre_translate(bbox.x(), bbox.y());
                let transform = if debug_bigbox {
                    bigbox_transform
                } else {
                    bigbox_transform
                        .post_translate(-adj_bbox.left() * render_mul, -adj_bbox.top() * render_mul)
                };
                if let Some(_) = resvg::render_node(&sprite, transform, &mut temp_canvas.as_mut()) {
                    if debug_dse {
                        _ = std::fs::write(
                            format!("debug/dse.s{}.png", j),
                            temp_canvas.encode_png().unwrap(),
                        );
                    }
                    // Notably, the hashing happens here, which amortizes the (sequential) shape_lookup.
                    shapeify(
                        temp_canvas,
                        V2(adj_bbox.left(), adj_bbox.top()),
                        render_mul,
                        sdf_border,
                    )
                } else {
                    None
                }
            } else {
                None
            }
        })
        .collect();
    if debug_dse {
        std::process::exit(0);
    }
    rendered
}
