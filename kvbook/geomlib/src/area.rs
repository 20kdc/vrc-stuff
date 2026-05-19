use crate::V2;
use core::ops::Sub;

#[derive(Clone, Copy, PartialEq, Eq, PartialOrd, Ord, Debug)]
pub struct Area1D<E: Copy> {
    pub min: E,
    pub max: E,
}

impl<E: Copy + Sub<Output = E>> Area1D<E> {
    /// Size of this area.
    #[inline]
    pub fn size(&self) -> E {
        self.max - self.min
    }
}
impl<E: Copy + PartialOrd> Area1D<E> {
    /// If some other area overlaps this one.
    #[inline]
    pub fn overlaps(&self, other: Area1D<E>) -> bool {
        other.min.lt(&self.max) && other.max.gt(&self.min)
    }
    #[inline]
    /// Overlapping region.
    pub fn overlap(&self, other: Area1D<E>) -> Option<Area1D<E>> {
        if self.overlaps(other) {
            Some(Area1D {
                min: if self.min.lt(&other.min) {
                    other.min
                } else {
                    self.min
                },
                max: if self.max.lt(&other.max) {
                    self.max
                } else {
                    other.max
                },
            })
        } else {
            None
        }
    }
    /// Carves a hole in an area using another area.
    /// Zero areas that would be generated are suppressed.
    /// If non-overlapping, self is given.
    #[inline]
    pub fn carve(&self, blade: Area1D<E>, handler: &mut impl FnMut(Self) -> ()) {
        let overlap = blade.overlap(*self);
        if let Some(overlap) = overlap {
            if overlap.min.gt(&self.min) {
                handler(Area1D {
                    min: self.min,
                    max: overlap.min,
                });
            }
            if overlap.max.lt(&self.max) {
                handler(Area1D {
                    min: overlap.max,
                    max: self.max,
                });
            }
        } else {
            handler(*self);
        }
    }
    #[inline]
    pub fn carve_vec(&self, blade: Self) -> Vec<Self> {
        let mut res = Vec::new();
        self.carve(blade, &mut |v| res.push(v));
        res
    }
}

#[derive(Clone, Copy, PartialEq, Eq, PartialOrd, Ord, Debug)]
pub struct Rect<E: Copy> {
    pub tl: V2<E>,
    pub br: V2<E>,
}

impl<E: Copy> Rect<E> {
    /// X area
    #[inline]
    pub fn area_x(&self) -> Area1D<E> {
        Area1D {
            min: self.tl.0,
            max: self.br.0,
        }
    }
    /// Y area
    #[inline]
    pub fn area_y(&self) -> Area1D<E> {
        Area1D {
            min: self.tl.1,
            max: self.br.1,
        }
    }
}
impl<E: Copy + Sub<Output = E>> Rect<E> {
    /// Size of this rectangle.
    pub fn size(&self) -> V2<E> {
        self.br - self.tl
    }
}
impl<E: Copy + PartialOrd> Rect<E> {
    pub fn overlap_x(&self, other: Rect<E>) -> Option<Area1D<E>> {
        self.area_x().overlap(other.area_x())
    }
    pub fn overlap_y(&self, other: Rect<E>) -> Option<Area1D<E>> {
        self.area_y().overlap(other.area_y())
    }
    /// Overlapping region.
    pub fn overlap(&self, other: Rect<E>) -> Option<Rect<E>> {
        if let Some(overlap_x) = self.overlap_x(other) {
            if let Some(overlap_y) = self.overlap_y(other) {
                Some(Self {
                    tl: V2(overlap_x.min, overlap_y.min),
                    br: V2(overlap_x.max, overlap_y.max),
                })
            } else {
                None
            }
        } else {
            None
        }
    }
    /// Same basic carving idea.
    /// This generates up to 4 overlapping rectangles.
    /// Alternatively, it can just return itself (if non-overlapping)
    #[inline]
    pub fn carve(&self, blade: Self, handler: &mut impl FnMut(Self) -> ()) {
        // if we don't overlap, then this would generate many outright redundant carves.
        // this is bad.
        if self.overlap(blade).is_none() {
            handler(*self);
            return;
        }
        self.area_x().carve(blade.area_x(), &mut |res_x| {
            handler(Rect {
                tl: V2(res_x.min, self.tl.1),
                br: V2(res_x.max, self.br.1),
            });
        });
        self.area_y().carve(blade.area_y(), &mut |res_y| {
            handler(Rect {
                tl: V2(self.tl.0, res_y.min),
                br: V2(self.br.0, res_y.max),
            });
        });
    }
    #[inline]
    pub fn carve_vec(&self, blade: Self) -> Vec<Self> {
        let mut res = Vec::new();
        self.carve(blade, &mut |v| res.push(v));
        res
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    #[test]
    pub fn test_area1d_carving() {
        let a: Area1D<usize> = Area1D { min: 0, max: 16 };
        let b: Area1D<usize> = Area1D { min: 4, max: 12 };
        let c: Area1D<usize> = Area1D { min: 0, max: 12 };
        let d: Area1D<usize> = Area1D { min: 4, max: 16 };
        assert_eq!(
            a.carve_vec(b),
            vec![Area1D { min: 0, max: 4 }, Area1D { min: 12, max: 16 },]
        );
        assert_eq!(b.carve_vec(a), vec![]);
        assert_eq!(a.carve_vec(c), vec![Area1D { min: 12, max: 16 }]);
        assert_eq!(a.carve_vec(d), vec![Area1D { min: 0, max: 4 }]);
    }
    #[test]
    pub fn test_rect_carving() {
        let a: Rect<usize> = Rect {
            tl: V2(0, 0),
            br: V2(16, 16),
        };
        let b: Rect<usize> = Rect {
            tl: V2(4, 4),
            br: V2(12, 12),
        };
        assert_eq!(
            a.carve_vec(b),
            vec![
                Rect {
                    tl: V2(0, 0),
                    br: V2(4, 16)
                },
                Rect {
                    tl: V2(12, 0),
                    br: V2(16, 16)
                },
                Rect {
                    tl: V2(0, 0),
                    br: V2(16, 4)
                },
                Rect {
                    tl: V2(0, 12),
                    br: V2(16, 16)
                },
            ]
        );
    }
}
