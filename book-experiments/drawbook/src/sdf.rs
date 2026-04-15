use crate::DBShape;
use crate::geom::{Raster, V2};
use rayon::prelude::*;
use std::collections::BTreeSet;
use tiny_skia::{Pixmap, PremultipliedColorU8};

#[derive(Clone, Copy, PartialEq, Eq, Debug)]
enum SDFState {
    Idle(bool),
    /// Distance is represented as X and Y absolute distance.
    Mark(bool, V2<usize>),
}

#[inline]
fn sdf_state_combinator(here: SDFState, other: SDFState, rel: V2<usize>) -> SDFState {
    match (here, other) {
        // all A idle checks
        (SDFState::Idle(_), SDFState::Idle(_)) => here,
        (SDFState::Idle(true), SDFState::Mark(true, dist)) => SDFState::Mark(true, dist + rel),
        (SDFState::Idle(false), SDFState::Mark(false, dist)) => SDFState::Mark(false, dist + rel),
        // where idle/mark don't agree on bit
        (SDFState::Idle(true), SDFState::Mark(false, _)) => here,
        (SDFState::Idle(false), SDFState::Mark(true, _)) => here,
        // all A mark checks
        (SDFState::Mark(_, _), SDFState::Idle(_)) => here,
        (SDFState::Mark(a_bit, dist_here), SDFState::Mark(b_bit, dist_other)) => {
            if a_bit != b_bit {
                here
            } else {
                let sq_here = (dist_here.0 * dist_here.0) + (dist_here.1 * dist_here.1);
                let dist_other_adj = dist_other + rel;
                let sq_other =
                    (dist_other_adj.0 * dist_other.0) + (dist_other_adj.1 * dist_other_adj.1);
                if sq_other < sq_here {
                    SDFState::Mark(a_bit, dist_other_adj)
                } else {
                    here
                }
            }
        }
    }
}

/// SDF via cellular automata.
/// Note that the size may differ from the input if we 'think we can get away with it' (is_solid)
pub fn shape_to_sdf(shape: &DBShape) -> Raster<f32> {
    let sz = shape.size();
    if shape.is_solid() {
        // optimization: For solid rectangles, generate a single pixel 'solid rectangle' output.
        // this is 'technically' a valid representation, which is important for cases like the 'debug snooping' in the Godot viewer.
        // however there will likely be separate optimizations at play.
        return Raster::new_blank(V2(1, 1), f32::INFINITY);
    }
    // The state is made up of vertical and horizontal straight-line distances.
    let mut state: Raster<SDFState> = Raster::new_blank(shape.size(), SDFState::Idle(false));
    // We need queue swapping because otherwise it's possible for 'drilling' to occur.
    // What happens is basically like the worst possible version of an A*-style heuristic.
    // We quickly expand favouring the top-left direction, and then other information has to be 'filled in'.
    // Instead, we swap between a pair of set-queues, which should reduce (though maybe not totally eliminate) redundant polls.
    // Without 'flipping rasters' each iteration, we can't 100% guarantee 'CA atomicity'.
    // Luckily, we don't have to, it'll resolve eventually.
    let mut this_queue: BTreeSet<V2<usize>> = BTreeSet::new();
    let mut next_queue: BTreeSet<V2<usize>> = BTreeSet::new();
    // Setup edges & state-field.
    for y in 0..sz.1 {
        for x in 0..sz.0 {
            // this marks the interior as valid
            let v = shape.data().get_usize(V2(x, y), false);
            let xyv2 = V2(x as i32, y as i32);
            let n = shape.data().get_i32(xyv2 + V2(0, -1), false);
            let ne = shape.data().get_i32(xyv2 + V2(1, -1), false);
            let e = shape.data().get_i32(xyv2 + V2(1, 0), false);
            let se = shape.data().get_i32(xyv2 + V2(1, 1), false);
            let s = shape.data().get_i32(xyv2 + V2(0, 1), false);
            let sw = shape.data().get_i32(xyv2 + V2(-1, 1), false);
            let w = shape.data().get_i32(xyv2 + V2(-1, 0), false);
            let nw = shape.data().get_i32(xyv2 + V2(-1, -1), false);
            let edge_h = if v { !(w && e) } else { w || e };
            let edge_v = if v { !(n && s) } else { n || s };
            let edge_d = if v {
                !(ne && nw && se && sw)
            } else {
                ne || nw || ne || nw
            };
            if edge_h || edge_v || edge_d {
                if x > 0 {
                    next_queue.insert(V2(x - 1, y));
                }
                if y > 0 {
                    next_queue.insert(V2(x, y - 1));
                }
                if x < (sz.0 - 1) {
                    next_queue.insert(V2(x + 1, y));
                }
                if y < (sz.1 - 1) {
                    next_queue.insert(V2(x, y + 1));
                }
            }
            // There's a horizontal bias here, but I'm reasonably sure it can't spread.
            let res: SDFState = if edge_h {
                SDFState::Mark(v, V2(1, 0))
            } else if edge_v {
                SDFState::Mark(v, V2(0, 1))
            } else if edge_d {
                SDFState::Mark(v, V2(1, 1))
            } else {
                SDFState::Idle(v)
            };
            state[V2(x, y)] = res;
        }
    }
    // Run cellular automata.
    // We use this pre-allocated 'valid neighbours' set to avoid querying what doesn't exist.
    // Edge calculations that care about this were already solved above.
    // This encodes the absolute position and the absolute axis distance, so we don't need to figure that out.
    let mut valid_neighbours: Vec<(V2<usize>, V2<usize>)> = Vec::with_capacity(4);
    while !next_queue.is_empty() {
        std::mem::swap(&mut next_queue, &mut this_queue);
        while let Some(toc) = this_queue.pop_first() {
            valid_neighbours.clear();
            if toc.0 > 0 {
                valid_neighbours.push((V2(toc.0 - 1, toc.1), V2(1, 0)));
            }
            if toc.0 < (sz.0 - 1) {
                valid_neighbours.push((V2(toc.0 + 1, toc.1), V2(1, 0)));
            }
            if toc.1 > 0 {
                valid_neighbours.push((V2(toc.0, toc.1 - 1), V2(0, 1)));
            }
            if toc.1 < (sz.1 - 1) {
                valid_neighbours.push((V2(toc.0, toc.1 + 1), V2(0, 1)));
            }
            // find surroundings
            // note that while it's convenient to use all 8 directions for edge calculations, it isn't needed, and we want this hot loop to be fast
            let curr = state[toc];
            let mut adj = curr;
            for v in &valid_neighbours {
                adj = sdf_state_combinator(adj, state[v.0], v.1);
            }
            if adj != curr {
                state[toc] = adj;
                for v in &valid_neighbours {
                    next_queue.insert(v.0);
                }
            }
        }
    }
    // Convert integer distances to a proper f32 SDF
    let mut cvec: Vec<f32> = Vec::with_capacity(sz.0 * sz.1);
    for srcv in state.data() {
        cvec.push(match srcv {
            SDFState::Idle(true) => f32::INFINITY,
            SDFState::Idle(false) => f32::NEG_INFINITY,
            SDFState::Mark(bit, dist) => {
                let sq_here = (dist.0 * dist.0) + (dist.1 * dist.1);
                // This -0.5 evens out the 'gap' produced by the initial distance on the edge pixels.
                let v = (sq_here as f32).sqrt() - 0.5f32;
                if *bit { v } else { -v }
            }
        });
    }
    Raster::new(cvec, sz)
}

/// Makes an SDF from a shape.
/// Note that the pixmap may be smaller than what came in if the shape is i.e. solid.
pub fn sdf_to_pixmap(src: &Raster<f32>, step: i32) -> Pixmap {
    let sz = src.size();
    assert!(step > 0);
    let step_f32 = step as f32;
    let rows: Vec<Vec<PremultipliedColorU8>> = (0..sz.1)
        .into_par_iter()
        .map(|y| {
            let mut total: Vec<PremultipliedColorU8> = Vec::new();
            for x in 0..sz.0 {
                let mut here: f32 = src[V2(x, y)];
                // convert to step form
                here = (here * step_f32) + 127.5f32;
                let r = (here.clamp(0f32, 255f32) as i32).clamp(0, 255) as u8;
                total.push(PremultipliedColorU8::from_rgba(r, r, r, 255).unwrap());
            }
            total
        })
        .collect();
    let mut pixmap = Pixmap::new(sz.0 as u32, sz.1 as u32).unwrap();
    let mut idx = 0;
    for row in rows {
        for pixel in row {
            pixmap.pixels_mut()[idx] = pixel;
            idx += 1;
        }
    }
    pixmap
}

pub fn downscale_size(w: &Pixmap, fac: u32) -> V2<u32> {
    V2((w.width() / fac).max(1), (w.height() / fac).max(1))
}

pub fn scale_pixmap(pixmap: &Pixmap, w: V2<u32>, filter: tiny_skia::FilterQuality) -> Pixmap {
    let mut out = Pixmap::new(w.0, w.1).unwrap();
    out.draw_pixmap(
        0,
        0,
        pixmap.as_ref(),
        &tiny_skia::PixmapPaint {
            blend_mode: tiny_skia::BlendMode::Source,
            quality: filter,
            ..Default::default()
        },
        tiny_skia::Transform::identity().pre_scale(
            w.0 as f32 / pixmap.width() as f32,
            w.1 as f32 / pixmap.height() as f32,
        ),
        None,
    );
    out
}
