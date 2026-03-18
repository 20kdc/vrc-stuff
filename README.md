# vrc-stuff

Monorepo for 20kdc VRChat projects.

Includes:

* `baseq2`: For `kvtools` Q2BSP support.
* `kip32`: Compiling C (via RISC-V) to Udon (and beyond?)
* `kvassets`: Assets for 20kdc worlds.
* `kvresearch`: 'Research' package used to understand Udon + things that aren't stable enough for `kvtools`.
* `kvtools`: Non-asset world design utilities.
	* Q2BSP import, precompiled Udon support
* `KVToolsTB`: TrenchBroom config for `kvtools` Q2BSP.
* `kvworkarounds`: Personal workarounds for SDK issues.
* `mdbook`: <https://20kdc.github.io/vrc-stuff/>
* `udon`: Udon manipulation libraries in Rust.
* `vpm`: VPM repository.
* `vrc-sigmar`: An implementation of Sigmar's Garden (from [Zachtronics's Opus Magnum](https://store.steampowered.com/app/558990/Opus_Magnum/)) as an asset for VRChat worlds.
	* (A proper demo world for this asset is pending a lot of work that may not happen.)
* `vrc-textslidesystem`: **_DOESN'T WORK? AND IS NOT USED._** (I might replace this with the core systems from a world I'm publishing when I have the time to refactor everything, idk. Networking is hard). A system to, i.e. let people on a stage broadcast their prewritten announcements on a text display. (Text can also be input live.)

## Building

The build script used to be a shell script; it's now managed using <https://github.com/casey/just>.

Shell scripts are still used in some places, though.

The dependencies required depend on what you're working on:

* `kip32`: `riscv64-unknown-elf-gcc`, also see `udon`
* `mdbook`: mdBook, also see `udon`
* `udon`: Rust -- if updating `api_c.xz`, Python is needed
* `vrc-sigmar` (assuming you need to replace the UASM): See `kip32`.

## Licensing etc.

Each individual sub-project may have further relevant licensing considerations in its README.

To the best of my understanding, the COPYING file applies to the full current contents of this repository, but in the event my conclusions are incorrect, it is still important to review these details.

That in mind, where possible, the contents of this repository are released as so:

```
This is free and unencumbered software released into the public domain.

Anyone is free to copy, modify, publish, use, compile, sell, or
distribute this software, either in source code form or as a compiled
binary, for any purpose, commercial or non-commercial, and by any
means.

In jurisdictions that recognize copyright laws, the author or authors
of this software dedicate any and all copyright interest in the
software to the public domain. We make this dedication for the benefit
of the public at large and to the detriment of our heirs and
successors. We intend this dedication to be an overt act of
relinquishment in perpetuity of all present and future rights to this
software under copyright law.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT.
IN NO EVENT SHALL THE AUTHORS BE LIABLE FOR ANY CLAIM, DAMAGES OR
OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE,
ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR
OTHER DEALINGS IN THE SOFTWARE.

For more information, please refer to <http://unlicense.org>
```
