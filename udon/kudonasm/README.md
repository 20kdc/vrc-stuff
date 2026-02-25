# `kudonasm`: Udon Assembly, No Not That One

**This is mostly TODO right now, so you can think of it as a wishlist.**

`kudonasm`'s purpose is to implement an embeddable (inline-friendly) Udon assembler with a few tricks the original doesn't know.

The goals are:

* **Unified internal symbol namespace**: Part of `kudonast`. While mixing code and data symbols is theoretically useless, no reason to keep that complexity. Internal symbols are also not included in the output code.
* **Equates**: These can perform calculations.
* **Constant string/extern merging**: Having 'deep' extern support is rather useful.

## Concepts / Syntax Summary

`kudonasm` operates in `kudonast`'s environment. This means it uses `kudonast`'s 'internal symbol' handling.

However, it also supplies some of its own environmental details.

It deliberately diverges significantly from Udon Assembly.

To simplify implementation and integration with the rest of the toolset, it's theoretically a sequence of RON entities, but uses some trickery to keep the syntax easy to read.

Ultimately, each entity represents a specific instruction or pseudoinstruction, which is then applied to the assembly environment.

The result should look something like [src/card.ron](src/card.ron).

Key principles are:

* Embeddability. The assembler should be capable of providing inline assembly services to other crates.
	* In particular, `kudonasm` is intended to be used in `kip32` as part of a potential future rework.
* Mixed code/data. **There are no start/end directives.** Especially in regards to the embedded snippets, it's important that code and data be freely mixable.
* Reasonably easy to write.
* Simple implementation.
* Ability to express arbitrary OdinSerializer data.

## Symbols / Equates

`kudonasm` has two 'symbol' sets (the symbols written to the Udon symbol table don't really count): Internal symbols and equates.

Internal symbols are resolved during the emit/link process, while equates are resolved on-use (thus are assembly-time variables).

Equates 'override' internal symbols; if an equate is missing at the time of use, the internal symbol is assumed.

To be specific, whenever a symbol (unquoted identifier such as `example` that isn't being used as an enum variant/etc.) is used _outside of the symbol being manipulated by equate pseudoinstructions:_

* If the symbol matches an existing equate, what happens depends on context.
	* If an internal symbol is required, the equate must resolve exactly to an internal symbol, which is then used.
	* Otherwise, if an `UdonInt` is required, the equate is returned as the result.

By default, the equate `_` exists, resolving to the integer 0.

## Operands

Operands are the core value of `kudonasm`. They conceptually resolve to 64-bit signed integers.

Operands have the following forms:

* `[symbol]`, `[symbol, modifier...]` (can add modifiers arbitrarily)
	* The `_` equate is useful for calculations that don't follow this order.
	* Modifiers are applied in sequence. They are:
		* `add(operand)`
		* `sub(operand)`
		* `mul(operand)`
* `1234`: Integers become their corresponding values.
* `"example"`: Strings are handled according to the operand's 'affinity'. Affinities are:
	* `error`: Strings are not permitted.
	* `data`: Strings are resolved to constant strings. These are automatically reused.
	* `extern`: Strings are resolved to dedicated 'extern strings'. These do not overlap with data strings.
	* `char`: Strings must contain a single Unicode codepoint, which is returned.

<!--

 -------------------------- THE INSTRUCTION LISTING STARTS HERE ---------------

-->

## Code labels / Access

Code labels are declared by writing them as an 'instruction' with their access.

'Access' represents if the symbol ends up in the Udon symbol table, and if it's exported.

It's possible for kudonasm to avoid putting a symbol there _period,_ which helps to avoid the anti-aliasing regulation of the Udon symbol table.

The available levels of access, written as their code labels, are:

* `internal(sym)` / `_(sym)`: Not written to Udon symbol table.

There is also an 'honourable mention' to symbols declared as `local()`, which declares a unique name.

## Instructions

As a quick reference listing, the instructions of Udon are:

* `nop`, `pop`, `copy`: No operands (solely uses stack).
* `push(v)`, `annotation(v)`, `jump_indirect(v)`: Each has an operand with `data` affinity.
* `jump_if_false(target)`, `jump(target)`: Operand with `error` affinity (as jumping to data space makes little sense)
* `extern(ext_slot)`: Operand with `extern` affinity.

There are also a 'macroinstruction':

* `stop`: No operands, shorthand for `jump(0xFFFFFFFC)`, jumping to the conventional stop address.

## Declarations

### `var(sym, access, value)`

Declares a heap value.

    #[serde(rename = "var")]
    Var(KU2Symbol, KU2Access, KU2HeapSlot),

### `sync(sym, itype)` / `sync_prop(sym, prop, itype)`

Adds sync metadata.

`sync(sym, itype)` is equivalent to `sync_prop(sym, "this", itype)`.

Interpolation types are `none`, `linear`, and `smooth`.

```
var(Wobbliness, public, float(0))
sync(Wobbliness, linear)
```

### `update_order(order_val)`

Sets the update order. The operand is evaluted with `error` affinity, as strings make no sense here.

```
update_order(0)
```

### `net_event(sym, max_per_second, parameters)`

Declares network call metadata for the given function.

Note that a lack of network call metadata doesn't prevent network calls, though having network call metadata may allow them.

To deny network calls, either make the function non-`public` or add a `_` prefix and don't have network call metadata.

```
net_event(ShowMessage, 5, [(ShowMessage_Param1, "SystemString")])
```

### `rename_sym(from, "to")`

`rename_sym` is a way to have both a code and data Udon symbol with the same name 'by force'.

The `from` is resolved reasonably normally. All references to it in the Udon symbol tables are then rewritten to `to`.

Notably, this occurs **at the specific point in assembly this directive is used. As far as most of the assembler is concerned, the symbol is still `from`.**

To explain why this is necessary, the following wouldn't work:

```
Var(Routine, public, string("example"))
public(Routine)
```

But the following would:

```
Var(Routine_data, public, string("example"))
public(Routine_code)
rename_sym(Routine_data, "Routine")
rename_sym(Routine_code, "Routine")
```

And would generate the intended effect in the Udon symbol table.

### `package("name", ["dep", "dep"])`

For 'normal' assembly purposes, this does nothing.

However, this acts as a marker which may be used to 'cut up' a set of assembly files into different segments.

Practically, 'packages' would be split into packages and snippets.

Packages are invoked when snippets require them for the first time.

Snippets are invoked multiple times with different equates, and then the equate table is reverted afterwards.

```
package("example", [])

// common code/decls here...

package("snippet", ["example"])

// instance-specific code here...
```

## Equate Pseudoinstructions

### `equ(sym, operand)` / `equ_str(sym, affinity, operand)`

Sets an equate. An existing equate will be overridden.

`equ(sym, operand)` is short for `equ_str(sym, error, operand)`.

Since this manipulates equates, the symbol is not 're-evaluated'.

```
equ_str(message, data, "Hello, world!")
equ_str(_s2i_, extern, "Hello, world!")
```

### `local(sym)`

```
local()
```

### `undef(sym)`

    // equate
    #[serde(rename = "local")]
    Local(KU2Symbol),
    #[serde(rename = "undef")]
    Undef(KU2Symbol),
