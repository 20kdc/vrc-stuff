#![no_std]
#![no_main]

use core::panic::PanicInfo;
use kip32::kip32_syscall;

#[unsafe(no_mangle)]
fn _start() {
    kip32_syscall!(b"TestSyscallDispatch", 1);
    kip32_syscall!(b"TestSyscallDispatchTwo", 1);
}

#[panic_handler]
#[inline(never)]
fn panic(_info: &PanicInfo) -> ! {
    kip32_syscall!(b"RustPanic", 1);
    loop {}
}
