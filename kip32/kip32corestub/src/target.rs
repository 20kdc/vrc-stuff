use gdbstub::target::ext::base::{BaseOps, singlethread::SingleThreadBase};
use gdbstub::target::{Target, TargetError};
use gdbstub_arch::riscv::Riscv32;

#[derive(Clone, Default)]
pub struct Kip32CoreDump {
    pub pc: u32,
    pub x: [u32; 32],
    pub memory: Vec<u8>,
}

/// This links the coredump mechanism to GDB.
/// It's kept as a separate struct to ease any future code-moving.
pub struct Kip32StateGDBTarget(pub Kip32CoreDump);

impl Target for Kip32StateGDBTarget {
    type Arch = Riscv32;
    type Error = ();
    fn base_ops(&mut self) -> gdbstub::target::ext::base::BaseOps<'_, Self::Arch, Self::Error> {
        BaseOps::SingleThread(self)
    }
    fn guard_rail_implicit_sw_breakpoints(&self) -> bool {
        true
    }
}

impl SingleThreadBase for Kip32StateGDBTarget {
    fn read_registers(
        &mut self,
        regs: &mut <Self::Arch as gdbstub::arch::Arch>::Registers,
    ) -> gdbstub::target::TargetResult<(), Self> {
        regs.pc = self.0.pc;
        regs.x = self.0.x;
        Ok(())
    }
    fn write_registers(
        &mut self,
        _regs: &<Self::Arch as gdbstub::arch::Arch>::Registers,
    ) -> gdbstub::target::TargetResult<(), Self> {
        Err(TargetError::NonFatal)
    }
    fn read_addrs(
        &mut self,
        start_addr: <Self::Arch as gdbstub::arch::Arch>::Usize,
        data: &mut [u8],
    ) -> gdbstub::target::TargetResult<usize, Self> {
        for i in 0..data.len() {
            let addr = (i as u32).wrapping_add(start_addr) as usize;
            if addr >= self.0.memory.len() {
                return Ok(i);
            }
            data[i] = self.0.memory[addr];
        }
        Ok(data.len())
    }
    fn write_addrs(
        &mut self,
        _start_addr: <Self::Arch as gdbstub::arch::Arch>::Usize,
        _data: &[u8],
    ) -> gdbstub::target::TargetResult<(), Self> {
        Err(TargetError::NonFatal)
    }
}
