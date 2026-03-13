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

## Line Termination

RON apparently does not support reading a series of concatenated entities. For this reason, `kudonasm` uses what will be gracefully called 'ugly magic' to implement this.

The `kudonasm` parser essentially collects lines until one of two things happens:

* Parsing succeeds, in which case the parsed entity is added, the lines so far cleared, and the 'virtual line number offset' adjusted.
* An error of a type that isn't 'protected' occurs _twice in a row with the same details,_ in which case the error is returned. \
  In the code comments, this principle of allowing the error to occur twice is referred to as 'coyote time'.
	* The goal here is to check if the parser can't make further progress with more data. \
	  If it's still reading to the end of the stream, the error end position moves with it -- if not, the error end position remains static.

To be clear, this is something of a bodged compromise to allow a single statement to span multiple lines on what is otherwise meant to be a 'line by line' language.

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

* `symbol`: Symbol (short form). Note that the other forms prevent you from using their names as symbols.
* `SYM(symbol)`: Symbol.
* `C(value)`: Constant. See `var()` below for what `value` can be.
	* If the value is a string or an integer _resolvable at the time this line is assembled,_ the constant is shared between uses.
* `I(value)`: **Raw** constant integer.
	* Be wary of this. `push(I(0))` would push the index of the first heap slot, _which is almost never what you want._
	* It's best used, say, _inside_ a constant.
* `ADD(o1, o2)` / `SUB(o1, o2)` / `MUL(o1, o2)`: Performs maths. Keep in mind the same caveats as `I` -- you usually won't want to do this.
* `EXT(extern)`: Extern. These are allocated from a special pool since the Udon runtime overwrites them with their delegates as an optimization.
* `ORD(char)`: Character code, i.e. `ORD('A')`

<!--

 -------------------------- THE INSTRUCTION LISTING STARTS HERE ---------------

-->

## Code labels / Access

Code labels are declared by writing them as an 'instruction' with their access.

'Access' represents if the symbol ends up in the Udon symbol table, and if it's exported.

It's possible for kudonasm to avoid putting a symbol there _period,_ which helps to avoid the anti-aliasing regulation of the Udon symbol table.

The available levels of access, written as their code labels, are:

* `internal(sym)`: Not written to Udon symbol table.
* `elidable(sym)`: Only written to the Udon symbol table when emitting Udon Assembly.
* `symbol(sym)` / `_(sym)`: Written to Udon symbol table.
* `public(sym)`: Exported.

Where access isn't specified (`_(sym)` and `var(sym, val)`), it's `elidable`.

There is also an 'honourable mention' to symbols declared as `local()`, which declares a unique name.

## Instructions

As a quick reference listing, the instructions of Udon are:

* `nop`, `pop`, `copy`: No operands (solely uses stack).
* `push(v)`, `annotation(v)`, `jump_indirect(v)`: Each has an operand with `data` affinity.
* `jump_if_false(target)`, `jump(target)`: Operand with `error` affinity (as jumping to data space makes little sense)
* `extern(ext_slot)`: Operand with `extern` affinity.

There are also 'macroinstructions':

* `stop`: No operands, shorthand for `jump(0xFFFFFFFC)`, jumping to the conventional stop address.
* `copy_static(src, dst)`: Shorthand for `push(src)`, `push(dst)`, `copy`
	* Example: `copy_static(C(uint(1234)), some_uint)`
* `ext(id, [param...])`: Shorthand for `push(param)` on each parameter, followed by `extern(SYM(id))`.
	* Note the implication that `id` is always a symbol/equate.

## Declarations

### `var(sym, value)` / `var_internal(sym, value)` / `var_elidable(sym, value)` / `var_symbol(sym, value)` / `var_public(sym, value)`

Declares a heap value.

The value itself can be:

* `int(n)`, `uint(n)`, `short(n)`, `ushort(n)`, `byte(n)`, `sbyte(n)`, `long(n)`, `ulong(n)`: Fixed values; no operand evaluation
* `string(v)`, `char(v)`, `true`, `false`, `float(v)`, `double(v)`: Fixed values; no operand evaluation
	* Char in particular is written as `char('A')` (similar to `ORD`)
* `int_c(n)`, `uint_c(n)`, `short_c(n)`, `ushort_c(n)`, `byte_c(n)`, `sbyte_c(n)`, `long_c(n)`, `ulong_c(n)`, `char_c(n)`: Heap slot of the given type with the given operand as value.
	* Importantly, the value here can refer to i.e. a code symbol.
* `null(type)`, `this(type)`: with the given `UdonType`.
* `type(type)`: `System.Type` (represented via `UdonType`).
* `ast(type, value)`: Fixed `UdonType` and `kudonast::UdonHeapValue`

An `UdonType` is written as a string, such as `"SystemString"`.

```
var(MyInteger, int(0))
```

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

## Meta

### `package("name", ["dep", "dep"])` / `package_end`

This 'cuts up' a set of assembly files into different segments.

Practically, 'packages' would be split into packages and snippets.

Snippets are invoked multiple times with different equates, and then the equate table is reverted afterwards.

_**Code which runs snippets should be extremely careful to invoke dependencies early.**_

Packages end between files, or when `package_end` is used.

```
package("example", [])

// common code/decls here...

package("snippet", ["example"])

// instance-specific code here...
```

### `code_comment(text)` / `data_comment(text)`

Inserts a comment into output UASM.

```
code_comment("Test")

data_comment("Test")
```

## Equate Pseudoinstructions

### `equ(sym, operand)`

Sets an equate. An existing equate will be overridden.

Since this manipulates equates, the symbol is not 're-evaluated'.

```
equ(message, C(string("Hello, world!")))
equ(_ecall_ext_char2str, EXT("SystemConvert.__ToString__SystemChar__SystemString"))
```

### `local(sym)`

Defines an equate between a symbol and a unique name.

Pairs well with `undef(sym)`.

```
local(sym)
```

### `undef(sym)`

Undefines an equate.

Pairs well with `local(sym)`.

```
undef(sym)
```
