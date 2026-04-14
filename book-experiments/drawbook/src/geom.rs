use core::ops::{Add, AddAssign, Div, DivAssign, Mul, MulAssign, Sub, SubAssign};

/// Two-element vector.
#[derive(Clone, Copy)]
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

impl<E: Copy> Raster<E> {
    pub fn new(data: Vec<E>, size: V2<usize>) -> Self {
        assert_eq!(data.len(), size.0 * size.1);
        Self { data, size }
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
}
