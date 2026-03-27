# MicroPython port

MicroPython port for kip32.

Due to issues with what does and doesn't work with MicroPython's build structure, this port has to be **copied** into MicroPython's `ports` directory. :(

This port is very likely **unreliable for serious purposes** and **very scuffed**. However, it's MicroPython! _In VRChat!_

## Licensing Considerations

This port pulls in the following extmods and other files, which need their copyright notices attached:

* `extmod/modjson.c`
* `extmod/moductypes.c`
* `extmod/modtime.c`
* `shared/readline/readline.c`
* `shared/runtime/pyexec.c`

However, it no longer uses picolibc, which should be a significant help.