//! Responsible for reporting progress.

use std::io::Write;
use std::sync::Mutex;

static STAGE_CURRENT: Mutex<&'static str> = Mutex::new("");

pub struct ProgressImpl;

impl booklib::progress::Progress for ProgressImpl {
    fn stage(&self, stage: &'static str) {
        let mut lock = STAGE_CURRENT.lock().unwrap();
        let mut stdout = std::io::stderr().lock();
        _ = write!(stdout, "\n{}", stage);
        _ = stdout.flush();
        *lock = stage;
    }
    fn status(&self, status: &str) {
        let mut stdout = std::io::stderr().lock();
        _ = write!(stdout, "\r{}{} ", *STAGE_CURRENT.lock().unwrap(), status);
        _ = stdout.flush();
    }
    fn alert(&self, status: &str) {
        let mut stdout = std::io::stderr().lock();
        _ = write!(stdout, "\n ** {} **\n", status);
        _ = stdout.flush();
    }
}

pub use booklib::progress::percentage;
