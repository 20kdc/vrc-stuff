use crate::DBShape;
use tiny_skia::{Pixmap, PremultipliedColorU8};

#[derive(Clone, Copy, PartialEq, Eq)]
enum SDFState {
    Idle,
    Edge,
    Hot,
    HotEdge,
    Invalid,
}

/// Makes an SDF from a shape.
/// It's implied in Valve's paper (SIGGRAPH '07, Chris Green, Valve Corporation) that they use a brute-force search.
/// Instead of doing that, we use a cellular automata.
/// UPDATE: YEAH NO DON'T DO THAT.
/// TODO: We need the gaussian distance field for things to really render right.
/// Also, we should be multithreading shape conversion.
pub fn shape_to_sdf(shape: &DBShape, step: i32) -> Pixmap {
    assert!(step > 0);
    let sz = shape.size();
    let fixup = shape.border(1);
    // these are the size of fixup
    let mut data: Vec<u8> = Vec::new();
    let mut state: Vec<SDFState> = Vec::new();
    let mut queued: Vec<(usize, usize)> = Vec::new();
    for _ in 0..fixup.data().len() {
        data.push(0);
        state.push(SDFState::Invalid);
    }
    for y in 0..sz.1 {
        for x in 0..sz.0 {
            let basis = fixup.index(x + 1, y + 1);
            let v = fixup.data()[basis];
            let n = fixup.data()[basis - fixup.w()];
            let s = fixup.data()[basis + fixup.w()];
            let w = fixup.data()[basis - 1];
            let e = fixup.data()[basis + 1];
            // this marks the interior as valid
            state[basis] = SDFState::Idle;
            if v {
                if !(n && s && w && e) {
                    data[basis] = 128;
                    state[basis] = SDFState::HotEdge;
                    queued.push((x, y));
                }
            }
        }
    }
    while let Some(toc) = queued.pop() {
        let mut spread = false;
        // find surroundings
        let basis = fixup.index(toc.0 + 1, toc.1 + 1);
        let bn = basis - fixup.w();
        let bs = basis + fixup.w();
        let bw = basis - 1;
        let be = basis + 1;
        match state[basis] {
            SDFState::Hot => {
                let p = fixup.data()[basis];
                let vn = data[bn] as i32;
                let vs = data[bs] as i32;
                let vw = data[bw] as i32;
                let ve = data[be] as i32;
                // based on the state of this pixel, we want to either be higher than surroundings or lower.
                let r = if p {
                    vn.min(vs).min(vw).min(ve) + step
                } else {
                    vn.max(vs).max(vw).max(ve) - step
                };
                let val = r.clamp(0, 255) as u8;
                if val != data[basis] {
                    data[basis] = val;
                    spread = true;
                }
                state[basis] = SDFState::Idle;
            }
            SDFState::HotEdge => {
                spread = true;
                state[basis] = SDFState::Edge;
            }
            _ => {
                // why are we even here?
            }
        }
        if spread {
            // x spread
            if state[bw] == SDFState::Idle {
                queued.push((toc.0 - 1, toc.1));
                state[bw] = SDFState::Hot;
            }
            if state[be] == SDFState::Idle {
                queued.push((toc.0 + 1, toc.1));
                state[be] = SDFState::Hot;
            }
            // y spread
            if state[bn] == SDFState::Idle {
                queued.push((toc.0, toc.1 - 1));
                state[bn] = SDFState::Hot;
            }
            if state[bs] == SDFState::Idle {
                queued.push((toc.0, toc.1 + 1));
                state[bs] = SDFState::Hot;
            }
        }
    }
    let mut pixmap = Pixmap::new(sz.0 as u32, sz.1 as u32).unwrap();
    for y in 0..sz.1 {
        for x in 0..sz.0 {
            let v = data[fixup.index(x + 1, y + 1)];
            pixmap.pixels_mut()[shape.index(x, y)] =
                PremultipliedColorU8::from_rgba(v, v, v, 255).unwrap();
        }
    }
    pixmap
}

pub fn scale_pixmap(pixmap: Pixmap, w: u32, h: u32) -> Pixmap {
    let mut out = Pixmap::new(w, h).unwrap();
    out.draw_pixmap(
        0,
        0,
        pixmap.as_ref(),
        &tiny_skia::PixmapPaint {
            blend_mode: tiny_skia::BlendMode::Source,
            quality: tiny_skia::FilterQuality::Bicubic,
            ..Default::default()
        },
        tiny_skia::Transform::identity().pre_scale(
            w as f32 / pixmap.width() as f32,
            h as f32 / pixmap.height() as f32,
        ),
        None,
    );
    out
}
