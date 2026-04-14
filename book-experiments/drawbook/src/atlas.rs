use crate::geom::{Rect, V2};
use rayon::prelude::*;

/// Atlas page planner.
pub struct AtlasPage {
    /// Current size of the atlas page.
    /// This can be dynamically increased as needed.
    pub size: usize,
    /// Rectangles already placed.
    pub rects: Vec<Rect<usize>>,
    /// Attachment points to try.
    pub points: Vec<V2<usize>>,
}

impl AtlasPage {
    /// Places something on the atlas page. Returns the resulting position.
    pub fn place(&mut self, wanted: V2<usize>) -> Option<V2<usize>> {
        let acceptable: Vec<V2<usize>> = self
            .points
            .par_iter()
            .filter_map(|p| {
                let rect = Rect {
                    tl: *p,
                    br: *p + wanted,
                };
                if rect.br.0 > self.size || rect.br.1 > self.size {
                    return None;
                }
                for v in &self.rects {
                    let overlaps_x = v.overlaps_x(rect);
                    let overlaps_y = v.overlaps_y(rect);
                    if overlaps_x && overlaps_y {
                        return None;
                    }
                    // there was going to be a more involved optimization here
                    // but it's been scrapped.
                }
                Some(*p)
            })
            .collect();
        if acceptable.is_empty() {
            None
        } else {
            let rct = Rect {
                tl: acceptable[0],
                br: acceptable[0] + wanted,
            };
            self.rects.push(rct);
            // create new points at the right, diagonal, and bottom
            self.points.push(V2(rct.tl.0, rct.br.1));
            self.points.push(V2(rct.br.0, rct.tl.1));
            self.points.push(rct.br);
            Some(acceptable[0])
        }
    }
    /// Cleans the attachment points list.
    /// It might be wise to do this after every place call.
    pub fn clean_points(&mut self) {
        let prev: Vec<V2<usize>> = self
            .points
            .par_iter()
            .filter_map(|p| {
                let rect = Rect {
                    tl: *p,
                    br: *p + V2(1, 1),
                };
                for v in &self.rects {
                    if v.overlaps(rect) {
                        return None;
                    }
                }
                Some(*p)
            })
            .collect();
        self.points = prev;
    }
}
