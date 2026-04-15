use core::ops::{Add, AddAssign, Div, DivAssign, Mul, MulAssign, Sub, SubAssign};

/// Two-element vector.
#[derive(Clone, Copy, PartialEq, Eq, PartialOrd, Ord, Debug)]
pub struct V2<E: Copy>(pub E, pub E);

impl<E: Copy + Add> Add for V2<E>
where
    E::Output: Copy,
{
    type Output = V2<E::Output>;
    fn add(self, rhs: Self) -> Self::Output {
        V2(self.0 + rhs.0, self.1 + rhs.1)
    }
}

impl<E: Copy + Sub> Sub for V2<E>
where
    E::Output: Copy,
{
    type Output = V2<E::Output>;
    fn sub(self, rhs: Self) -> Self::Output {
        V2(self.0 - rhs.0, self.1 - rhs.1)
    }
}

impl<E: Copy + Div> Div for V2<E>
where
    E::Output: Copy,
{
    type Output = V2<E::Output>;
    fn div(self, rhs: Self) -> Self::Output {
        V2(self.0 / rhs.0, self.1 / rhs.1)
    }
}

impl<E: Copy + Mul> Mul for V2<E>
where
    E::Output: Copy,
{
    type Output = V2<E::Output>;
    fn mul(self, rhs: Self) -> Self::Output {
        V2(self.0 * rhs.0, self.1 * rhs.1)
    }
}

impl<E: Copy + AddAssign> AddAssign for V2<E> {
    fn add_assign(&mut self, rhs: Self) {
        self.0 += rhs.0;
        self.1 += rhs.1;
    }
}

impl<E: Copy + SubAssign> SubAssign for V2<E> {
    fn sub_assign(&mut self, rhs: Self) {
        self.0 -= rhs.0;
        self.1 -= rhs.1;
    }
}

impl<E: Copy + MulAssign> MulAssign for V2<E> {
    fn mul_assign(&mut self, rhs: Self) {
        self.0 *= rhs.0;
        self.1 *= rhs.1;
    }
}

impl<E: Copy + DivAssign> DivAssign for V2<E> {
    fn div_assign(&mut self, rhs: Self) {
        self.0 /= rhs.0;
        self.1 /= rhs.1;
    }
}

/// Raster (mutable)
#[derive(Clone)]
pub struct Raster<E: Copy> {
    data: Vec<E>,
    size: V2<usize>,
}

impl<E: Copy> std::ops::Index<V2<usize>> for Raster<E> {
    type Output = E;
    fn index(&self, index: V2<usize>) -> &E {
        assert!(index.0 < self.size.0);
        &self.data[index.0 + (index.1 * self.size.0)]
    }
}

impl<E: Copy> std::ops::IndexMut<V2<usize>> for Raster<E> {
    fn index_mut(&mut self, index: V2<usize>) -> &mut E {
        assert!(index.0 < self.size.0);
        &mut self.data[index.0 + (index.1 * self.size.0)]
    }
}

impl<E: Copy + PartialEq> PartialEq for Raster<E> {
    fn eq(&self, other: &Self) -> bool {
        (self.size == other.size) && self.data.eq(&other.data)
    }
}

impl<E: Copy + Eq> Eq for Raster<E> {}

impl<E: Copy> Raster<E> {
    pub fn new(data: Vec<E>, size: V2<usize>) -> Self {
        assert_eq!(data.len(), size.0 * size.1);
        Self { data, size }
    }
    pub fn new_blank(size: V2<usize>, base: E) -> Self {
        let mut data = Vec::new();
        for _ in 0..(size.0 * size.1) {
            data.push(base);
        }
        Self { data, size }
    }
    pub fn size(&self) -> V2<usize> {
        self.size
    }
    pub fn data(&self) -> &[E] {
        &self.data
    }
    pub fn data_mut(&mut self) -> &mut [E] {
        &mut self.data
    }
    pub fn get_usize(&self, pos: V2<usize>, def: E) -> E {
        if pos.0 >= self.size.0 || pos.1 >= self.size.1 {
            def
        } else {
            self.data[pos.0 + (pos.1 * self.size.0)]
        }
    }
    pub fn set_usize(&mut self, pos: V2<usize>, val: E) {
        self.data[pos.0 + (pos.1 * self.size.0)] = val;
    }
    pub fn get_i32(&self, pos: V2<i32>, def: E) -> E {
        if pos.0 < 0 || pos.1 < 0 {
            def
        } else {
            self.get_usize(V2(pos.0 as usize, pos.1 as usize), def)
        }
    }
    pub fn set_i32(&mut self, pos: V2<i32>, val: E) {
        assert!(pos.0 >= 0 && pos.1 >= 0);
        self.set_usize(V2(pos.0 as usize, pos.1 as usize), val);
    }
    pub fn inbounds_i32(&mut self, pos: V2<i32>) -> bool {
        (pos.0 >= 0 && ((pos.0 as usize) < self.size.0))
            && (pos.1 >= 0 && ((pos.1 as usize) < self.size.1))
    }
    /// Blits to this raster from another raster.
    pub fn copy_i32(&mut self, src: &Self, pos: V2<i32>) {
        for j in 0..src.size.1 {
            let ty = pos.1 + (j as i32);
            if ty < 0 || ty >= (self.size.1 as i32) {
                continue;
            }
            for i in 0..src.size.0 {
                let tx = pos.1 + (i as i32);
                if tx < 0 || tx >= (self.size.0 as i32) {
                    continue;
                }
                self.set_i32(V2(tx, ty), src.data[i + (j * src.size.0)]);
            }
        }
    }
    /// Extracts a copy of a subset of the contents of this raster.
    /// Uses pos/size notation.
    /// We do this super naively for now, grumble grumble.
    pub fn extract_i32(&mut self, pos: V2<i32>, size: V2<usize>, oob: E) -> Self {
        let mut res = Self::new_blank(size, oob);
        res.copy_i32(self, pos * V2(-1, -1));
        res
    }
}

impl<E: Copy + Eq> Raster<E> {
    /// Checks if an area is filled with the given element.
    /// Uses absolute bounds notation.
    /// May panic or return wrong results if out of bounds.
    pub fn area_eq_usize(&self, tl: V2<usize>, br: V2<usize>, e: E) -> bool {
        for j in tl.1..br.1 {
            for i in tl.0..br.0 {
                if self.data[i + (j * self.size.0)] != e {
                    return false;
                }
            }
        }
        true
    }
    /// Finds absolute-bounds-notation for a crop rectangle
    pub fn find_crop_rectangle(&self, e: E) -> (V2<usize>, V2<usize>) {
        let mut potential_crop_left = 0;
        while potential_crop_left < self.size().0 {
            if !self.area_eq_usize(
                V2(potential_crop_left, 0),
                V2(potential_crop_left + 1, self.size.1),
                e,
            ) {
                break;
            }
            potential_crop_left += 1;
        }

        let mut potential_crop_right = self.size.0;
        while potential_crop_right > potential_crop_left {
            if !self.area_eq_usize(
                V2(potential_crop_right - 1, 0),
                V2(potential_crop_right, self.size.1),
                e,
            ) {
                break;
            }
            potential_crop_right -= 1;
        }

        let mut potential_crop_top = 0;
        while potential_crop_top < self.size.1 {
            if !self.area_eq_usize(
                V2(0, potential_crop_top),
                V2(self.size.0, potential_crop_top + 1),
                e,
            ) {
                break;
            }
            potential_crop_top += 1;
        }

        let mut potential_crop_bottom = self.size.1;
        while potential_crop_bottom > potential_crop_top {
            if !self.area_eq_usize(
                V2(0, potential_crop_bottom - 1),
                V2(self.size.0, potential_crop_bottom),
                e,
            ) {
                break;
            }
            potential_crop_bottom -= 1;
        }

        (
            V2(potential_crop_left, potential_crop_top),
            V2(potential_crop_right, potential_crop_bottom),
        )
    }
}

#[derive(Clone, Copy)]
pub struct Rect<E: Copy> {
    pub tl: V2<E>,
    pub br: V2<E>,
}

impl<E: Copy + Sub<Output = E>> Rect<E> {
    /// Size of this rectangle.
    pub fn size(&self) -> V2<E> {
        self.br - self.tl
    }
}
impl<E: Copy + PartialOrd> Rect<E> {
    /// If some other rectangle overlaps this one.
    pub fn overlaps(&self, other: Rect<E>) -> bool {
        self.overlaps_x(other) && self.overlaps_y(other)
    }
    /// If some other rectangle overlaps this one on the X axis.
    pub fn overlaps_x(&self, other: Rect<E>) -> bool {
        other.tl.0.lt(&self.br.0) && other.br.0.gt(&self.tl.0)
    }
    /// If some other rectangle overlaps this one on the Y axis.
    pub fn overlaps_y(&self, other: Rect<E>) -> bool {
        other.tl.1.lt(&self.br.1) && other.br.1.gt(&self.tl.1)
    }
}
