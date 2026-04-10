#![no_std]
#![no_main]

use core::panic::PanicInfo;
use kip32::udon::UdonValue;
use kip32::udon::types::SystemString;
use kip32::{kip32_export, kip32_syscall};

kip32::kip32_udon_val!(Example, SystemString, b"C(\"\")");

#[kip32_export("_start")]
fn start() {
    Example::push();
    kip32_syscall!(b"TestSyscallDispatch", 1);
    kip32_syscall!(b"TestSyscallDispatchTwo", 1);
}

#[panic_handler]
#[inline(never)]
fn panic(_info: &PanicInfo) -> ! {
    kip32_syscall!(b"RustPanic", 1);
    loop {}
}
