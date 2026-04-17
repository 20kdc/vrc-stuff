use crate::geom::{Rect, V2};
use rayon::prelude::*;

/// Atlas page planner.
pub struct AtlasPage {
    /// Current size of the atlas page.
    /// This can be dynamically increased as needed.
    pub size: V2<usize>,
    /// Rectangles already placed.
    pub rects: Vec<Rect<usize>>,
    /// Attachment points to try.
    /// Note the second part, the 'diagonal flag'.
    /// This is because diagonal placements are usually inefficient.
    pub points: Vec<(V2<usize>, bool)>,
}

impl AtlasPage {
    /// Places something on the atlas page. Returns the resulting position.
    pub fn place(&mut self, wanted: V2<usize>) -> Option<V2<usize>> {
        let mut acceptable: Vec<(V2<usize>, bool)> = self
            .points
            .par_iter()
            .filter_map(|(p, diagonal)| {
                let rect = Rect {
                    tl: *p,
                    br: *p + wanted,
                };
                if rect.br.0 > self.size.0 || rect.br.1 > self.size.1 {
                    return None;
                }
                for v in &self.rects {
                    let overlaps_x = v.overlap_x(rect).is_some();
                    let overlaps_y = v.overlap_y(rect).is_some();
                    if overlaps_x && overlaps_y {
                        return None;
                    }
                    // there was going to be a more involved optimization here
                    // but it's been scrapped.
                }
                Some((*p, *diagonal))
            })
            .collect();
        if acceptable.is_empty() {
            None
        } else {
            // really shouldn't be sorting the whole list, grr.
            // notably, we DON'T want to do this in clean_points, as it's useful to have earlier (thus 'tighter-spaced') points show up first.
            acceptable.sort_by_key(|v| v.1);
            let rct = Rect {
                tl: acceptable[0].0,
                br: acceptable[0].0 + wanted,
            };
            self.rects.push(rct);
            // create new points at the right, diagonal, and bottom
            self.points.push((V2(rct.tl.0, rct.br.1), false));
            self.points.push((V2(rct.br.0, rct.tl.1), false));
            self.points.push((rct.br, true));
            Some(acceptable[0].0)
        }
    }
    /// Makes the atlas a step larger.
    pub fn enlarge(&mut self) {
        if self.size.0 > self.size.1 {
            self.size.1 *= 2;
        } else {
            self.size.0 *= 2;
        }
    }
    /// Cleans the attachment points list.
    /// It might be wise to do this after every place call.
    pub fn clean_points(&mut self) {
        let prev: Vec<(V2<usize>, bool)> = self
            .points
            .par_iter()
            .filter(|(p, _)| {
                let rect = Rect {
                    tl: *p,
                    br: *p + V2(1, 1),
                };
                for v in &self.rects {
                    if v.overlap(rect).is_some() {
                        return false;
                    }
                }
                true
            })
            .map(|v| *v)
            .collect();
        self.points = prev;
    }
}
