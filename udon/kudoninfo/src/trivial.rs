pub mod interpolations {
    pub const NONE: u64 = 0;
    pub const LINEAR: u64 = 1;
    pub const SMOOTH: u64 = 2;
}

/// Every interpolation in Udon, in uasm form, as a sparse table (see [sparse_table_get]).
pub static UDON_INTERPOLATIONS: &[Option<&'static str>] =
    &[Some("none"), Some("linear"), Some("smooth")];
