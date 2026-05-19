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
