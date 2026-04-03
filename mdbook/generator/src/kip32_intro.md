# 'kip32': Writing Udon Behaviours in C and Rust

'kip32' is a toolset designed to ahead-of-time recompile RISC-V instructions into Rust.

The goal here is to allow using C and Rust to write behaviours with the minimum possible overhead.

More details are at [the repo proper](https://github.com/20kdc/vrc-stuff/blob/main/kip32/).

A TLDR of how things have gone is:

While results have been decently successful, and it powers a world [running MicroPython in VRChat,](https://vrchat.com/home/world/wrld_d57a1730-5a48-4774-81c7-e1c84c223692/), the integration between kip32 code and everything outside of it is pretty poor.

Due to how extensive the extern list is, IDE integrations don't fare well with the large amount of wrappers necessary to directly expose every Udon type.

Since I only have 16GB of RAM, and I kind of need to be able to continue developing the project to continue developing it, I have chosen not to go this route. (Besides, there isn't really much of a good solution for C anyway.)

kip32's Rust SDK may in future provide procedural macros to bridge this gap.
