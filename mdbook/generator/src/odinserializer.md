# Odin Serializer

Most of the Udon program is encoded using a vendored OdinSerializer.

Somewhat conveniently, `Packages/com.vrchat.worlds/Runtime/Udon/Serialization/OdinSerializer/Version.txt` tells us _which_ version.

As of this writing, commit [`8d9fc0bca118d9c6f927ee2fb23330138a99cbf2`](https://github.com/TeamSirenix/odin-serializer/tree/8d9fc0bca118d9c6f927ee2fb23330138a99cbf2) is used. _For this section, file references will be made relative to that repository._

To find differences, it's easiest to clone <https://github.com/TeamSirenix/odin-serializer>, checkout the appropriate commit, delete the OdinSerializer directory in the checkout, copy the OdinSerializer directory from the VRChat Worlds package to the checkout, then remove all `.meta` files up to 4 directories deep in the checkout.

The main points of note are:

1. Any 'risky' Formatters (used to serialize/deserialize various types of object) have been disabled via various methods. A particular focus has been on removing weak references.
2. There are some API changes owing to the removal of weak references.
3. There are various performance tweaks.

Importantly, _**there is no difference in the serialization format.**_

Therefore, what follows is a summary of the OdinSerializer core format, based on the upstream code.

## Concepts

Conceptually, OdinSerializer data is stored as a stream of _entries,_ with a final 'end of stream' entry delimiting serialization.

There are three different kinds of entries:

* Entries that encode values or the beginning of compound values. (I call these 'Values'.)
	* These entries are evidenced by having Named and Unnamed variants.
	* The compound values encodable this way are References (objects) and Structs (value types).
		* The `StartOfReferenceNode` values have Node IDs, which are are particularly important; these match up to the `InternalReference` values.
		* On the C# side, the contents of these compounds are encoded using Formatters; this is the Serializer/Formatter barrier.
* Entries solely used inside other compounds that don't properly decode to values alone.
	* `PrimitiveArray` and `StartOfArray` are good examples of this. They don't have the named/unnamed tagging, and they're reference types anyway, so they need to live in a reference node.
* Entries that end compound nodes (`EndOfNode` and `EndOfArray`).

The data reader/writer implementation has a lot of flexibility in how it _arrives_ at these entries, and there is a notion of a tree structure built within the 'flat' entry list using the `StartOf` and `EndOf` entries.

Still, it's possible to read/write entries without knowing the higher-level types they're used to construct, and the entry types are arranged such that it's similarly possible to read/write the entry _tree_ without that knowledge.

**However, a 'sensible' interpretation of things like i.e. array length will lead to failure.** See SerializationFormatter notes below.

The reading of an entry is divided into the _header_ (reads name, type) and the _content_ (anything specific to that entry type).

When reading an entry, `PeekEntry` is used to read the header, and then the appropriate read function is used to read the content.

`SkipEntry` in [`BaseDataReader`](https://github.com/TeamSirenix/odin-serializer/blob/8d9fc0bca118d9c6f927ee2fb23330138a99cbf2/OdinSerializer/Core/DataReaderWriters/BaseDataReader.cs#L397), meanwhile, skips over the overall tree structure.

To summarize how this fits together, observe the following tree:

* Serializer: Operates at entry level. Specialized formatters are used for anything encoded _in a single entry._ Has access to field information.
	* All base integer types are serializers.
	* ComplexTypeSerializer: Encodes null directly, or wraps in start-of-reference-node or start-of-struct-node as appropriate.
		* Assuming there is something to encode, finds the appropriate formatter and passes control to the formatter layer.
		* **ComplexTypeSerializer is used for all reference types except String.**
* Formatter: Operates _within_ a reference or struct node. Implements encoding/decoding the contents.

A serializer is chosen using the _field's type_ (and has to worry about propagating the field name) while a formatter is chosen using the _object's type_ (and doesn't).

Reference/struct serializers wrap the formatters with the appropriate start/end entries. (Note, however, it can theoretically be a complete free-for-all on named/unnamed fields _inside_ the node.)

Another problem to keep in mind is that there's no distinction between a named and unnamed array. This is because arrays are reference types -- **all reference types except `String` are wrapped appropriately.** (OdinSerializer treats String as a value type for encoding purposes.)

The following example trace shows how an array field is encoded:

```
Value(Some("ExportedSymbols"), StartRefNode(TypeID(13), 22)),
StartOfArray(2),
Value(None, Primitive(String("message"))),
Value(None, Primitive(String("syncMe"))),
EndOfArray,
EndOfNode,
```

## `SerializableFormatter`

For some reason, it's common to see types use the following arrangement:

```
Value(None, StartRefNode(TypeName(18, "VRC.Udon.Common.UdonSyncProperty, VRC.Udon.Common"), 27)),
StartOfArray(2),
Value(Some("type"), Primitive(String("System.String, mscorlib"))),
Value(Some("Name"), Primitive(String("this"))),
Value(Some("type"), Primitive(String("VRC.Udon.Common.Interfaces.UdonSyncInterpolationMethod, VRC.Udon.Common"))),
Value(Some("InterpolationAlgorithm"), Primitive(ULong(1))),
EndOfArray,
EndOfNode,
```

This is the fault of `SerializableFormatter`, which, as a format goes, looks like a chip in an otherwise very pretty glass vase.

## Binary Format

The OdinSerializer binary format is little-endian.

For the binary format (the one we're interested in), the format follows this exact pattern; the file can be precisely reduced to a reasonably flat list of entries.

The type of each entry is defined by a single byte at the start of that entry. Udon programs, for instance, start with `0x02: UnnamedStartOfReferenceNode`.

The binary entry types are listed at [`BinaryEntryType`](https://github.com/TeamSirenix/odin-serializer/blob/8d9fc0bca118d9c6f927ee2fb23330138a99cbf2/OdinSerializer/Core/DataReaderWriters/Binary/BinaryEntryType.cs).

Note the Named/Unnamed distinction. Also note `TypeName`/`TypeID` -- these are not _really_ entry types at all, but live in an overlapping namespace of 'entry types' valid when reading a Type.

With all of this in mind, the functions to pay attention to in [`BinaryDataReader`](https://github.com/TeamSirenix/odin-serializer/blob/8d9fc0bca118d9c6f927ee2fb23330138a99cbf2/OdinSerializer/Core/DataReaderWriters/Binary/BinaryDataReader.cs) are:

* L93: `PeekEntry`: Reads the header of any type of entry.
	* The header only contains the entry type byte, and (for any `Named*` entry type) a string value for the field name.
* L1823: `ReadStringValue`: Defines the format of a string.
	* Reads a byte, and then an int32. If the byte is 0, the string is written in 8-bit Latin-1 (`U+0000` through `U+00FF`); if the byte is 1, it is written in little-endian UTF-16. The int32 represents the length of the string in characters.
* L1953: `SkipPeekedEntryContent`: Reads the content of any type of entry.
	* Reference nodes contain a type entry followed by an int32 node ID. Negative node IDs don't count.
	* Struct nodes contain a type entry.
	* Array starts contain a length int64.
	* Primitive arrays contain a length int32 and a bytes-per-element int32. These are multiplied to the final length.
	* Primitive types in general are their obvious sizes.
	* Internal references and external index references are int32 node IDs.
	* GUIDs, external references using GUIDs, and 'Decimal' values, are 8 bytes.
	* Strings and external references using strings contain string values.
	* Nulls and end-of entries have no contents.
* L2092: `ReadTypeEntry`: Defines the type format.
	* The type format starts with an 'entry type' byte. In practice that the byte here shares namespace with entry types seems to be more to avoid defining another enumeration.
	* If the byte is `UnnamedNull`, null is returned.
	* If the byte is `TypeID`, the type is read using an int32 type ID; no further data is given.
	* If the byte is `TypeName`, an int32 type ID is read (to cache the type in), and a string value is read (the name of the type).
	* Otherwise, it's invalid.
