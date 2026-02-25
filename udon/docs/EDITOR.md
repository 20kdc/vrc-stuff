# The Udon Editing Workmess: A Ramble

The Udon code has a lot of abstract base classes with increasingly questionable decisions.

`UdonBehaviour`, for instance, derives from `AbstractUdonBehaviour`, which implements `IUdonBehaviour`, which extends interfaces `IUdonEventReceiver`, `IUdonProgramVariableAccessTarget`, and `IUdonSyncTarget`.

This, however, is ultimately harmless (except for the effect on Udon types caused by rewriting everything to `IUdonEventReceiver` for some reason).

The real horror is program sources and program code in general.

`UdonBehaviour` leads a double life.

In the VRChat client, program sources do not exist, period. This is rather important, since they're built using custom, potentially user-defined types that the client doesn't have.

In the editor, however, the `AbstractUdonProgramSource programSource` field exists.

## Udon Program Assets

What does exist is `AbstractSerializedUdonProgramAsset serializedProgramAsset`. The questionable thing about this field, in case you were wondering, is that there isn't actually a way for the `AbstractSerializedUdonProgramAsset` to ever, in fact, _not_ be `SerializedUdonProgramAsset`, but there's also much ado about `IUdonSignatureHolder`, which just...

I'm used to the idea of using an abstract base class solely as documentation for permitted API surface area, but then this is just throwing in casts for the sake of it, surely?

With how it's used, would it not make more sense for `IUdonSignatureHolder` to simply be part of the abstract base class?

I'm sure there's no dire consequences from this, at lea- wait, why _are_ there signatures on Udon code???

Where are these signatures coming from? _**When are they being added into the files on disk?!?!?!?**_

Ok, so `IUdonSignatureVerifier` is **really** just a fancy way of saying `UdonManager` (see what I mean about too many interfaces???), which then verifies using a key from some mysterious place. But the signatures are in my local files!

After much ado, it turns out they're patching the `SerializedUdonProgramAsset` objects while you aren't looking during export based on `UdonSignatureHolderMarker`. The key is generated locally during export.

Why? I don't know. Was someone maliciously replacing Udon code on-disk to gain a foothold?

**Thankfully,** this being done during export means that it will 'only' (and I use these quotes with purpose) effectively guarantee re-serialization of `SerializedUdonProgramAsset`, no matter where you hide it.

Ultimately, the outcome of this is that **precompiled Udon programs stored as `SerializedUdonProgramAsset` cannot be checked into version control without thrashing issues.**

But we really want to use `SerializedUdonProgramAsset` to reduce the update complexity when VRChat put another field in there rather than in IUdonProgram for some reason.

As a possible fix, if we were, say, to disguise the source `SerializedUdonProgramAsset` as something _else..._ well, that's why Unity's JSON system is very likely going to be involved.

## Program Sources

So the program source system is worse than it should be.

`AbstractUdonProgramSource` mostly makes sense: the field which is The Program (`AbstractSerializedUdonProgramAsset`), `RefreshProgram`, and `RunEditorUpdate` (which lets you control the public variables, etc.).

With these core principles, you could essentially implement whatever you want!

...The problem is that `DrawPublicVariables`, `DrawInteractionArea` etc. live in `UdonProgramAsset`.

Realistically, you have a 'choice': Cripple your public variables UI, copy/paste proprietary VRChat code, or subclass from `UdonProgramAsset`.
