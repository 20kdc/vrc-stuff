use crate::geom::{Rect, V2};
use rayon::prelude::*;
use std::collections::BTreeSet;

/// Atlas page planner.
pub struct AtlasPage {
    /// Current size of the atlas page.
    /// This may be freely changed.
    pub size: V2<usize>,
    /// Delete free areas with X or Y under this size.
    /// Change this if you always add a border, so you'll never request a rectangle under this size.
    /// This can also be used as a performance tweak.
    pub delete_under: V2<usize>,
    /// Free areas to try.
    pub free: BTreeSet<Rect<usize>>,
}

impl AtlasPage {
    pub fn new(initial_size: V2<usize>) -> Self {
        let mut free = BTreeSet::new();
        free.insert(Rect {
            tl: V2(0, 0),
            br: V2(usize::MAX, usize::MAX),
        });
        Self {
            size: initial_size,
            delete_under: V2(1, 1),
            free,
        }
    }

    /// Marks an area as used.
    /// In practice, this works by carving out of the unused area.
    pub fn mark(&mut self, area: Rect<usize>) {
        let prev: BTreeSet<Rect<usize>> = self
            .free
            .par_iter()
            .map(|rct| {
                let mut res: Vec<Rect<usize>> = Vec::with_capacity(4);
                rct.carve(area, &mut |f| {
                    if f.size().0 < self.delete_under.0 {
                        return;
                    }
                    if f.size().1 < self.delete_under.1 {
                        return;
                    }
                    res.push(f);
                });
                res
            })
            .flatten()
            .collect();
        self.free = prev;
    }

    /// Forcibly deletes half of the free areas to improve performance.
    pub fn perf_chop(&mut self) {
        let mut v: Vec<Rect<usize>> = self.free.iter().map(|v| *v).collect();
        v.sort_by_key(|rct| rct.size().0.min(rct.size().1));
        self.free.clear();
        for vi in (v.len() >> 1)..(v.len()) {
            self.free.insert(v[vi]);
        }
    }

    /// Places something on the atlas page and calls [Self::mark]. Returns the resulting position.
    pub fn place(&mut self, wanted: V2<usize>) -> Option<V2<usize>> {
        let acceptable: Vec<V2<usize>> = self
            .free
            .par_iter()
            .filter_map(|free_rect| {
                let mut effective_br = free_rect.br;
                effective_br.0 = effective_br.0.min(self.size.0);
                effective_br.1 = effective_br.1.min(self.size.1);
                let rect = Rect {
                    tl: free_rect.tl,
                    br: free_rect.tl + wanted,
                };
                if rect.br.0 > effective_br.0 || rect.br.1 > effective_br.1 {
                    return None;
                }
                Some(free_rect.tl)
            })
            .collect();
        if acceptable.is_empty() {
            None
        } else {
            let rct = Rect {
                tl: acceptable[0],
                br: acceptable[0] + wanted,
            };
            self.mark(rct);
            Some(rct.tl)
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
}
