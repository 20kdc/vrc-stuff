//! So Skia's premultiplication obsession is a bit of a problem for preventing 'dark haloing' in non-Skia surfaces.
//! Also it's a pain to switch structs all the time. And also it seems to be having problems with higher SDF downscale values.
//! So this is a custom scaler that knows when to leave well alone.

use crate::{Raster, V2};

/// Core sequence scaler.
/// This algorithm takes a sequence of one length and converts it to a sequence of another length.
/// The algorithm is expected to be 'box filter-like'.
pub fn scale_core<E: Copy>(
    src: &[E],
    len: usize,
    dst: &mut Vec<E>,
    mixer: &dyn Fn(&[(E, f32)]) -> E,
) {
    if src.len() == 0 {
        // make stuff up
        let blank = mixer(&[]);
        for _ in 0..len {
            dst.push(blank);
        }
        return;
    }
    if len == 0 {
        return;
    }
    if len == src.len() {
        dst.extend_from_slice(src);
        return;
    }
    // This is the 'pure' ratio, the one used for box sampling.
    let r = (src.len() as f32) / (len as f32);
    if r < 1f32 {
        // source is smaller than target (magnification)
        // we don't really try to be clever here, we use a linear upscale
        let rs = ((src.len() - 1) as f32) / ((len - 1) as f32);
        for i in 0..len {
            let source_xf = (i as f32) * rs;
            let subpixel = source_xf.fract();
            let left_idx = source_xf.floor() as usize;
            let right_idx = source_xf.ceil() as usize;
            let left = src[left_idx.max(0).min(src.len() - 1)];
            let right = src[right_idx.max(0).min(src.len() - 1)];
            // standard lerp function. not much to say here
            dst.push(mixer(&[(left, 1.0f32 - subpixel), (right, subpixel)]))
        }
    } else {
        // source is larger than target (minification)
        // therefore the target samples areas of R (ratio) source pixels.
        // consider the following diagram:
        // +--+--+--+--+
        // |  |  |  |  |
        // +--+--+--+--+
        // |   |   |   |
        // +---+---+---+
        let mut sampler: Vec<(E, f32)> = Vec::new();
        for i in 0..len {
            // the left edge of each box is neatly defined by i * R
            let box_left = (i as f32) * r;
            // and it follows that the right edge is defined as the left edge of the next box
            let box_right = ((i + 1) as f32) * r;
            // we now compute the start/end range that we're going to add to the sampler.
            let region_start = (box_left.floor() as usize).max(0).min(src.len() - 1);
            let region_end = (box_right.ceil() as usize).max(0).min(src.len());
            // add to sampler
            for j in region_start..region_end {
                // j represents a specific pixel covered by the box.
                // j is j's start, but j's end is j + 1.
                let covered_start = (j as f32).max(box_left);
                let covered_end = ((j + 1) as f32).min(box_right);
                let coverage = covered_end - covered_start;
                let pix = src[j];
                if coverage > 0.0f32 {
                    // we divide coverage by the box filter width, which is again r, to ensure a proper total
                    sampler.push((pix, coverage / r));
                }
            }
            // apply the sampler, then clear it for next round
            dst.push(mixer(&sampler));
            sampler.clear();
        }
    }
}

// We implement scaling in a separate file because it's a whole Thing.
impl<E: Copy> Raster<E> {
    /// Scaler.
    /// We implement scaling in a two-pass fashion as this massively simplifies the mathematics involved.
    pub fn scale(&self, size: V2<usize>, mixer: &dyn Fn(&[(E, f32)]) -> E) -> Raster<E> {
        if self.size() == size {
            self.clone()
        } else if self.size().0 == size.0 {
            // width eq.
            self.scale_y(size.1, mixer)
        } else if self.size().1 == size.1 {
            // height eq.
            self.scale_x(size.0, mixer)
        } else {
            self.scale_x(size.0, mixer).scale_y(size.1, mixer)
        }
    }
    fn scale_x(&self, w: usize, mixer: &dyn Fn(&[(E, f32)]) -> E) -> Raster<E> {
        let szn = V2(w, self.size().1);
        let mut data: Vec<E> = Vec::with_capacity(szn.0 * szn.1);
        let srcw = self.size().0;
        for y in 0..szn.1 {
            let base = y * srcw;
            scale_core(&self.data()[base..(base + srcw)], w, &mut data, mixer);
        }
        Raster::new(data, szn)
    }
    fn scale_y(&self, h: usize, mixer: &dyn Fn(&[(E, f32)]) -> E) -> Raster<E> {
        let szn = V2(self.size().0, h);
        let blank = mixer(&[]);
        let mut data = Raster::new_blank(szn, blank);
        let srch = self.size().1;
        let mut temp_src: Vec<E> = Vec::with_capacity(srch);
        let mut temp_dst: Vec<E> = Vec::with_capacity(h);
        for x in 0..szn.0 {
            // grab column
            for y in 0..srch {
                temp_src.push(self[V2(x, y)]);
            }
            // scale
            scale_core(&temp_src, h, &mut temp_dst, mixer);
            // apply
            for y in 0..h {
                data[V2(x, y)] = temp_dst[y];
            }
            // clear buffers for next round
            temp_src.clear();
            temp_dst.clear();
        }
        data
    }
}

#[cfg(test)]
mod tests {
    fn f32mixer(mix: &[(f32, f32)]) -> f32 {
        let mut total = 0f32;
        for v in mix {
            total += v.0 * v.1;
        }
        total
    }
    #[test]
    fn integrity() {
        let mut tmpseq: Vec<f32> = Vec::new();
        // upscale should preserve some sensible invariants
        super::scale_core(&[0f32, 1f32], 4, &mut tmpseq, &f32mixer);
        assert_eq!(tmpseq, [0f32, 0.33333333f32, 0.66666666f32, 1f32]);
        // downscale should preserve basic box filter invariant
        tmpseq.clear();
        super::scale_core(&[0f32, 1f32, 4f32, 7f32], 2, &mut tmpseq, &f32mixer);
        assert_eq!(tmpseq, [0.5f32, 5.5f32]);
    }
}
