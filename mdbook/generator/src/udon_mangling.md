# Name Mangling

Udon's API consists primarily of mangled type and function names from .NET types and functions.

## Types

Type mangling behaviour is publically described in `Packages/com.vrchat.worlds/Integrations/UdonSharp/Editor/Compiler/Udon/CompilerUdonInterface.cs`.

_It's important to keep in mind that this code is an independent reimplementation that VRChat 'adopted'._ (The 'actual rules' are likely handled in the wrapper module generator, which we don't have.)

Types are best described as 'smooshed together from parts'.

They can't really be understood as 'decodable', as they're arguably too ambiguous for that.

Instead, there's a number of cases to follow when encoding them:

1. If this is a ref or out parameter, _after everything else,_ add a `Ref` suffix.
	* Return types are not considered ref or out parameters.
	* This may or may not be a type rule, but type resolvers account for it as if it was one, and it's possible for this to be encoded in C#'s reflection.
2. If this is an array, encode the element type, and then add an `Array` suffix.
3. If this is a generic parameter, its type is called `T`.
	* Note that this is the only way to tell from an extern signature _alone_ if it is generic. It is somewhat easier to determine this from the type metadata in the node definitions.
	* Node definition parameter information does not handle generics, and instead uses concrete types. Therefore, a generic parameter is one where the the encoded type in the extern does not match that of the node definition parameter.
4. For the general case, remove all `.` (namespace dots) and `+` (nested types) from the type name.
	* If this type has generic arguments (i.e. is specialized), they are encoded and appended. A quirk applies here.

There's also a number of 'quirks':

* While some code will tell you `VRC.Udon.UdonBehaviour` is replaced with `VRC.Udon.Common.Interfaces.IUdonEventReceiver`, the answer is much more complicated than that.
	* TLDR: `IUdonEventReceiver` is the real type.
	* Basically, the SDK3 assemblies can't have `UdonBehaviour` because it lives in end-user-visible code, which is further down the dependency ladder. So they typically use `IUdonEventReceiver` as their proxy type. That is to say, these APIs really are referring to `IUdonEventReceiver`.
	* But, Udon-targetting languages would prefer you thought it was called `UdonBehaviour`. For this reason they use various approaches to hack in this specific type alias.
* As UdonSharp code helpfully points out, `SystemCollectionsGenericListT` and `SystemCollectionsGenericIEnumerableT` specifically are shortened to remove `SystemCollectionsGeneric`.

There are very probably errors in this guide.

## Externs

Externs follow a set and defined format.

We'll use `VRCSDK3DataDataDictionary.__TryGetValue__VRCSDK3DataDataToken_VRCSDK3DataDataTokenRef__SystemBoolean` as an example.

The format is best described as a parse tree, which will be shown here with bullet points:

* Wrapper/Method split
	* Wrapper module: `VRCSDK3DataDataDictionary`: This is the name of the _wrapper module,_ which _usually,_ but not always, matches the Udon type name.
	* `.`: This dot, of which there is always exactly one, splits the wrapper module from the method.
	* Qualified method: `__TryGetValue__VRCSDK3DataDataToken_VRCSDK3DataDataTokenRef__SystemBoolean`: The fully-qualified method itself.
		* Method name: `__TryGetValue__`: Method name, as-is (except for special cases), surrounded with `__`.
		* Parameters: `VRCSDK3DataDataToken_VRCSDK3DataDataTokenRef`: The parameters to the function, separated with `_`.
			* `out` and `ref` parameters both become the `Ref` suffix.
			* Notably, these do not include the 'this' parameter, which may or may not exist.
				* The existence of a 'this' parameter can be inferred by the difference between the parameter metadata and the extern.
		* Return type separator: `__` (Always present; see return type)
		* Return type: `SystemBoolean`: Describes an out parameter. If `SystemVoid`, no return parameter exists.

### Special Method Names

The `__surrounded__` method name can have two special values:

* `__ctor__`: Constructors (aka `new Example(...)`).
* `__op_Equality__` (and similar): The corresponding operators. There's a few of these, and I don't have a full list.
	* There appears to either be some confusion between `Logical` and `Bitwise` operators, or C#'s implementation of the operators is unusual somehow.
