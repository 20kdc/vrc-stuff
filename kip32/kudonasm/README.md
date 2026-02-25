# `kudonasm`: Udon Assembly, No Not That One

**This is all TODO right now, so you can think of it as a wishlist.**

`kudonasm`'s purpose is to implement an embeddable (inline-friendly) Udon assembler with a few tricks the original doesn't know.

The goals are:

* **Unified internal symbol namespace**: Part of `kudonast`. While mixing code and data symbols is theoretically useless, no reason to keep that complexity. Internal symbols are also not included in the output code.
* **Equates**: These can perform calculations.
* **Constant string/extern merging**: Having 'deep' extern support is rather useful.

## Concepts

`kudonasm` operates in `kudonast`'s environment. This means it uses `kudonast`'s 'internal symbol' handling.

However, it also supplies some of its own environmental details (equates support).

## Syntax

The syntax of `kudonasm` deliberately diverges significantly from Udon Assembly.

To simplify implementation and integration with the rest of the toolset, it's theoretically a sequence of RON entities, but uses some trickery to keep the syntax easy to read.

Ultimately, each entity represents a specific instruction or pseudoinstruction, which is then applied to the assembly environment.

The result should look something like this:

```
/*
 * data and code can mix freely
 * symbols aren't quoted
 * access is divided into 'internal', 'symbol' and 'public'
 * newlines are used as implicit separators
 */
var (message, public, "SystemString", P(String("hello
world")))
sync (message, this, none)
// equates expose UdonInt ops
equ (example, Add(I(1), I(2)))
// or can be used to shorthand externs
equ_extern (ext_log, "UnityEngineDebug.__Log__SystemObject__SystemVoid")
// code labels use the access as their opening keyword, except 'internal' can also be '_'
public(_interact)
    // instruction operands are:
    // * [label]
    // * "constant string/extern"
    // * 0x1234 // numbers
    push ([message])
    extern ([ext_log])
    jump (0xFFFFFFFC)
_(infinite_loop)
    // parsing will add in each line until a complete entity is found
    // this can lead to unexpected errors if i.e. commas are missing
    jump ([
        infinite_loop,
    ])
// assorted
update_order (0)
net_event (Test, 5, [(Test_Param1, "SystemString")])
```
