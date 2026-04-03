/// Core of the bytes concatenator. Note that the maths occurs in the macro.
pub const fn kip32_bytesconcat_core<const NA: usize, const NB: usize, const NR: usize>(
    a: &[u8],
    b: &[u8],
) -> [u8; NR] {
    let mut res: [u8; NR] = [0; NR];
    let mut i = 0;
    while i < NA {
        res[i] = a[i];
        i += 1;
    }
    i = 0;
    while i < NB {
        res[i + NA] = b[i];
        i += 1;
    }
    res
}

/// Concatenates two constant byte arrays at compile-time.
/// kip32 metadata is heavily reliant on compile-time string concatenation, so we need this.
/// Hypothetically, it could instead be faked with carefully engineered repr(C) structs.
/// This would have no useful purpose.
#[macro_export]
macro_rules! kip32_bytesconcat {
    ($a:expr, $b:expr) => {
        &$crate::kip32_bytesconcat_core::<{ $a.len() }, { $b.len() }, { ($a.len()) + ($b.len()) }>(
            $a, $b,
        )
    };
}

/// Metadata has to be aligned for the JAL trick to work.
/// It doesn't cost us anything to align **all** metadata this way; so do it.
#[repr(C)]
#[repr(align(4))]
pub struct Kip32Metadata<const N: usize>(pub [u8; N]);

/// Declares a .kip32_metadata entry.
#[macro_export]
macro_rules! kip32_metadata {
    ($name:ident, $content:expr) => {
        // note: it doesn't matter if this isn't *really* used, since it's gone in the final binary anyway
        // meanwhile, having global metadata disappear suddenly would be really bad!
        #[used]
        #[unsafe(link_section = ".kip32_metadata")]
        static $name: $crate::Kip32Metadata<{ $crate::kip32_bytesconcat!($content, b"\0").len() }> =
            $crate::Kip32Metadata(*$crate::kip32_bytesconcat!($content, b"\0"));
    };
}

#[macro_export]
macro_rules! kip32_syscall {
    ($content:expr) => {
        {
            $crate::kip32_metadata!(CALLME, $crate::kip32_bytesconcat!(b"syscall:", $content));
            let a0: usize;
            let a1: usize;
            let a2: usize;
            let a3: usize;
            let a4: usize;
            let a5: usize;
            let a6: usize;
            let a7: usize;
            unsafe {
                core::arch::asm!(
                    "jal {}",
                    sym CALLME,
                    out("a0") a0,
                    out("a1") a1,
                    out("a2") a2,
                    out("a3") a3,
                    out("a4") a4,
                    out("a5") a5,
                    out("a6") a6,
                    out("a7") a7,
                    clobber_abi("C"),
                );
            }
            (a0, a1, a2, a3, a4, a5, a6, a7)
        }
    };
    ($content:expr, $a0:expr) => {
        {
            $crate::kip32_metadata!(CALLME, $crate::kip32_bytesconcat!(b"syscall:", $content));
            let a0: usize;
            let a1: usize;
            let a2: usize;
            let a3: usize;
            let a4: usize;
            let a5: usize;
            let a6: usize;
            let a7: usize;
            unsafe {
                core::arch::asm!(
                    "jal {}",
                    sym CALLME,
                    inout("a0") $a0 => a0,
                    out("a1") a1,
                    out("a2") a2,
                    out("a3") a3,
                    out("a4") a4,
                    out("a5") a5,
                    out("a6") a6,
                    out("a7") a7,
                    clobber_abi("C")
                );
            }
            (a0, a1, a2, a3, a4, a5, a6, a7)
        }
    };
    ($content:expr, $a0:expr, $a1:expr) => {
        {
            $crate::kip32_metadata!(CALLME, $crate::kip32_bytesconcat!(b"syscall:", $content));
            let a0: usize;
            let a1: usize;
            let a2: usize;
            let a3: usize;
            let a4: usize;
            let a5: usize;
            let a6: usize;
            let a7: usize;
            unsafe {
                core::arch::asm!(
                    "jal {}",
                    sym CALLME,
                    inout("a0") $a0 => a0,
                    inout("a1") $a1 => a1,
                    out("a2") a2,
                    out("a3") a3,
                    out("a4") a4,
                    out("a5") a5,
                    out("a6") a6,
                    out("a7") a7,
                    clobber_abi("C")
                );
            }
            (a0, a1, a2, a3, a4, a5, a6, a7)
        }
    };
    ($content:expr, $a0:expr, $a1:expr, $a2:expr) => {
        {
            $crate::kip32_metadata!(CALLME, $crate::kip32_bytesconcat!(b"syscall:", $content));
            let a0: usize;
            let a1: usize;
            let a2: usize;
            let a3: usize;
            let a4: usize;
            let a5: usize;
            let a6: usize;
            let a7: usize;
            unsafe {
                core::arch::asm!(
                    "jal {}",
                    sym CALLME,
                    inout("a0") $a0 => a0,
                    inout("a1") $a1 => a1,
                    inout("a2") $a2 => a2,
                    out("a3") a3,
                    out("a4") a4,
                    out("a5") a5,
                    out("a6") a6,
                    out("a7") a7,
                    clobber_abi("C")
                );
            }
            (a0, a1, a2, a3, a4, a5, a6, a7)
        }
    };
    ($content:expr, $a0:expr, $a1:expr, $a2:expr, $a3:expr) => {
        {
            $crate::kip32_metadata!(CALLME, $crate::kip32_bytesconcat!(b"syscall:", $content));
            let a0: usize;
            let a1: usize;
            let a2: usize;
            let a3: usize;
            let a4: usize;
            let a5: usize;
            let a6: usize;
            let a7: usize;
            unsafe {
                core::arch::asm!(
                    "jal {}",
                    sym CALLME,
                    inout("a0") $a0 => a0,
                    inout("a1") $a1 => a1,
                    inout("a2") $a2 => a2,
                    inout("a3") $a3 => a3,
                    out("a4") a4,
                    out("a5") a5,
                    out("a6") a6,
                    out("a7") a7,
                    clobber_abi("C")
                );
            }
            (a0, a1, a2, a3, a4, a5, a6, a7)
        }
    };
    ($content:expr, $a0:expr, $a1:expr, $a2:expr, $a3:expr, $a4:expr) => {
        {
            $crate::kip32_metadata!(CALLME, $crate::kip32_bytesconcat!(b"syscall:", $content));
            let a0: usize;
            let a1: usize;
            let a2: usize;
            let a3: usize;
            let a4: usize;
            let a5: usize;
            let a6: usize;
            let a7: usize;
            unsafe {
                core::arch::asm!(
                    "jal {}",
                    sym CALLME,
                    inout("a0") $a0 => a0,
                    inout("a1") $a1 => a1,
                    inout("a2") $a2 => a2,
                    inout("a3") $a3 => a3,
                    inout("a4") $a4 => a4,
                    out("a5") a5,
                    out("a6") a6,
                    out("a7") a7,
                    clobber_abi("C")
                );
            }
            (a0, a1, a2, a3, a4, a5, a6, a7)
        }
    };
    ($content:expr, $a0:expr, $a1:expr, $a2:expr, $a3:expr, $a4:expr, $a5:expr) => {
        {
            $crate::kip32_metadata!(CALLME, $crate::kip32_bytesconcat!(b"syscall:", $content));
            let a0: usize;
            let a1: usize;
            let a2: usize;
            let a3: usize;
            let a4: usize;
            let a5: usize;
            let a6: usize;
            let a7: usize;
            unsafe {
                core::arch::asm!(
                    "jal {}",
                    sym CALLME,
                    inout("a0") $a0 => a0,
                    inout("a1") $a1 => a1,
                    inout("a2") $a2 => a2,
                    inout("a3") $a3 => a3,
                    inout("a4") $a4 => a4,
                    inout("a5") $a5 => a5,
                    out("a6") a6,
                    out("a7") a7,
                    clobber_abi("C")
                );
            }
            (a0, a1, a2, a3, a4, a5, a6, a7)
        }
    };
    ($content:expr, $a0:expr, $a1:expr, $a2:expr, $a3:expr, $a4:expr, $a5:expr, $a6:expr) => {
        {
            $crate::kip32_metadata!(CALLME, $crate::kip32_bytesconcat!(b"syscall:", $content));
            let a0: usize;
            let a1: usize;
            let a2: usize;
            let a3: usize;
            let a4: usize;
            let a5: usize;
            let a6: usize;
            let a7: usize;
            unsafe {
                core::arch::asm!(
                    "jal {}",
                    sym CALLME,
                    inout("a0") $a0 => a0,
                    inout("a1") $a1 => a1,
                    inout("a2") $a2 => a2,
                    inout("a3") $a3 => a3,
                    inout("a4") $a4 => a4,
                    inout("a5") $a5 => a5,
                    inout("a6") $a6 => a6,
                    out("a7") a7,
                    clobber_abi("C")
                );
            }
            (a0, a1, a2, a3, a4, a5, a6, a7)
        }
    };
    ($content:expr, $a0:expr, $a1:expr, $a2:expr, $a3:expr, $a4:expr, $a5:expr, $a6:expr, $a7:expr) => {
        {
            $crate::kip32_metadata!(CALLME, $crate::kip32_bytesconcat!(b"syscall:", $content));
            let a0: usize;
            let a1: usize;
            let a2: usize;
            let a3: usize;
            let a4: usize;
            let a5: usize;
            let a6: usize;
            let a7: usize;
            unsafe {
                core::arch::asm!(
                    "jal {}",
                    sym CALLME,
                    inout("a0") $a0 => a0,
                    inout("a1") $a1 => a1,
                    inout("a2") $a2 => a2,
                    inout("a3") $a3 => a3,
                    inout("a4") $a4 => a4,
                    inout("a5") $a5 => a5,
                    inout("a6") $a6 => a6,
                    inout("a7") $a7 => a7,
                    clobber_abi("C")
                );
            }
            (a0, a1, a2, a3, a4, a5, a6, a7)
        }
    };
}
