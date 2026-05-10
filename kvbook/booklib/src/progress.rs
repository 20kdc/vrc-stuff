//! See [Progress].

/// Exposes a progress reporting interface to internal processes.
pub trait Progress: Send + Sync {
    fn stage(&self, stage: &'static str);
    fn status(&self, status: &str);
    fn alert(&self, status: &str);
}

pub fn percentage(pos: usize, len: usize) -> usize {
    (pos * 100) / len
}
