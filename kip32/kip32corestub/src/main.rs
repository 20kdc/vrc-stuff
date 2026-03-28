use gdbstub::common::Signal;
use gdbstub::stub::run_blocking::*;
use gdbstub::stub::{BaseStopReason, GdbStub, SingleThreadStopReason};
use gdbstub::target::Target;
use std::net::{TcpListener, TcpStream};

mod coredump_udon;
mod target;

struct EventLoop;

impl BlockingEventLoop for EventLoop {
    type Target = target::Kip32StateGDBTarget;
    type Connection = TcpStream;
    type StopReason = SingleThreadStopReason<u32>;

    fn wait_for_stop_reason(
        _target: &mut Self::Target,
        _conn: &mut Self::Connection,
    ) -> Result<
        Event<Self::StopReason>,
        WaitForStopReasonError<
            <Self::Target as Target>::Error,
            <Self::Connection as gdbstub::conn::Connection>::Error,
        >,
    > {
        Ok(Event::TargetStopped(BaseStopReason::Terminated(
            Signal::SIGILL,
        )))
    }

    fn on_interrupt(
        _target: &mut Self::Target,
    ) -> Result<Option<Self::StopReason>, <Self::Target as Target>::Error> {
        // we do absolutely nothing
        Ok(None)
    }
}

fn main() {
    // -- arg parsing --

    let mut filename: Option<String> = None;
    let mut arg_parser = lexopt::Parser::from_env();
    while let Some(arg) = arg_parser.next().expect("arg_parser") {
        match arg {
            lexopt::Arg::Value(val) => {
                if filename.is_some() {
                    panic!("Can't have two filenames");
                }
                filename = Some(val.into_string().unwrap());
            }
            _ => panic!("{:?}", arg.unexpected()),
        }
    }

    // -- setup --

    let res =
        std::fs::read(filename.expect("filename must be given")).expect("file must be readable");
    let coredump = coredump_udon::parse(&res).unwrap();

    println!("PC: {:08x} ", coredump.pc);
    println!("Memory: {:08x} bytes", coredump.memory.len());

    // --

    let sockaddr = "localhost:8192";
    println!(
        "Waiting for GDB: gdb-multiarch example.elf -ex \"target remote {}\"",
        sockaddr
    );
    let sock = TcpListener::bind(sockaddr).unwrap();
    let (stream, _) = sock.accept().unwrap();

    // --

    let gdbstub = GdbStub::new(stream);
    let mut target = target::Kip32StateGDBTarget(coredump);
    gdbstub.run_blocking::<EventLoop>(&mut target).unwrap();
}
