use crate::DBShape;
use crate::geom::V2;
use rayon::prelude::*;
use tiny_skia::{Pixmap, PremultipliedColorU8};

/// Makes an SDF from a shape.
/// Note that the pixmap may be smaller than what came in if the shape is i.e. solid.
pub fn shape_to_sdf(shape: &DBShape, step: i32) -> Pixmap {
    let sz = shape.size();
    if shape.is_solid() {
        // optimization: For solid rectangles, generate a single pixel 'solid rectangle' output.
        // this is 'technically' a valid representation, which is important for cases like the 'debug snooping' in the Godot viewer.
        // however there will likely be separate optimizations at play.
        let mut pixmap = Pixmap::new(1, 1).unwrap();
        pixmap.pixels_mut()[0] = PremultipliedColorU8::from_rgba(255, 255, 255, 255).unwrap();
        return pixmap;
    }
    assert!(step > 0);
    let neigh = 256 / step;
    let step_f32 = step as f32;
    // optimized neighbourhood scan order
    // notably, there's a rule here: distance from origin is always >= current
    // this allows for very quick aborts
    let mut scan_order: Vec<i32> = Vec::new();
    scan_order.push(0);
    for i in 0..(neigh + 1) {
        scan_order.push(i);
        scan_order.push(-i);
    }
    let rows: Vec<Vec<PremultipliedColorU8>> = (0..sz.1)
        .into_par_iter()
        .map(|y| {
            let mut total: Vec<PremultipliedColorU8> = Vec::new();
            for x in 0..sz.0 {
                let here = shape.data().get_usize(V2(x, y), false);
                // Distance in pixels to edge.
                // If we're filled, we're trying to find the nearest dark pixel, and our distance is 'positive'.
                // If we're unfilled, we're trying to find the nearest light pixel, and our distance is 'negative'.
                // Either way, we're trying to reduce dist 'downwards', so we use that here for simplicity and fix the sign later.
                let mut dist_sq = f32::INFINITY;
                for qy in &scan_order {
                    let qyad = (*qy as f32) * ((*qy) as f32);
                    if qyad > dist_sq {
                        break;
                    }
                    for qx in &scan_order {
                        let qxad = ((*qx) as f32) * ((*qx) as f32);
                        let pixdstsq = qxad + qyad;
                        if pixdstsq > dist_sq {
                            break;
                        }
                        let tgt = shape
                            .data()
                            .get_i32(V2(qx + (x as i32), qy + (y as i32)), false);
                        if tgt == !here {
                            // pixel of desired kind and close enough
                            dist_sq = pixdstsq;
                        }
                    }
                }
                // distance in pixels
                let mut adjf = dist_sq.sqrt();
                // technically, the edge exists between the pixels, not on them
                adjf -= 0.5f32;
                // become signed
                if !here {
                    adjf *= -1f32;
                }
                // convert to step form
                adjf = (adjf * step_f32) + 127.5f32;
                let r = (adjf.clamp(0f32, 255f32) as i32).clamp(0, 255) as u8;
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
