//! Responsible for reporting progress.

use std::io::Write;
use std::sync::Mutex;

static STAGE_CURRENT: Mutex<&'static str> = Mutex::new("");

pub fn stage(stage: &'static str) {
    let mut lock = STAGE_CURRENT.lock().unwrap();
    let mut stdout = std::io::stdout().lock();
    _ = write!(stdout, "\n{}", stage);
    _ = stdout.flush();
    *lock = stage;
}

pub fn status(status: &str) {
    let mut stdout = std::io::stdout().lock();
    _ = write!(stdout, "\r{}{} ", *STAGE_CURRENT.lock().unwrap(), status);
    _ = stdout.flush();
}

pub fn alert(status: &str) {
    let mut stdout = std::io::stdout().lock();
    _ = write!(stdout, "\n ** {} **\n", status);
    _ = stdout.flush();
}

pub fn percentage(pos: usize, len: usize) -> usize {
    (pos * 100) / len
}
