# Quake 2 BSP Import

This Quake 2 BSP importer allows creating static world geometry with, i.e. TrenchBroom and a more recent `ericw-tools` (such as one of the 2.0.0 alpha releases from <https://github.com/ericwa/ericw-tools/releases>).

The basic idea is that:

1. Install the package and look at `Setup.asset` in Unity, **which automates the following:**
	1. Install `TrenchBroom~/KVToolsTB` to inside TrenchBroom's user directory (Linux: `$HOME/.TrenchBroom/games/KVToolsTB`)
	2. Adjust `CompilationProfiles.cfg` to point at ericw-tools QBSP.
	3. Copy `TrenchBroom~/KDCBSPGameRoot` to `Assets/` of your Unity project.
		* Note that your game root may _actually_ be anywhere; KDCBSP intentionally does not care about this.
			* It's usually easiest to include everything in your Unity project in a single workspace. This prevents needing to switch the TrenchBroom game root.
			* `Assets/KDCBSPGameRoot/DefaultWorkspaceConfig.asset` is the hardcoded default workspace config if no workspace is otherwise set.
2. Add materials to the `KDCBSPGameRoot/materials` directory to add materials in Unity.
	* Be sure to press the "Update Quake VFS" button on your workspace when you change materials!
		* This does some setup so that TrenchBroom can see your materials. You may need to restart TrenchBroom.
	* More precise configuration can be added by creating KDCBSP material config files of the same name.
	* If a material's 'icon' can't be generated, KDCBSP may give a 'no icon' image to TrenchBroom. The 'icon' is the image used in TrenchBroom as the texture.
		* In this case, you may want to add a PNG file (named the same as the material) to override the material's TrenchBroom icon.
3. Add prefabs to the `KDCBSPGameRoot/progs` directory to add new entity types.
	* You can add `KDCBSPEntityDescriptor` to set default brush entity parameters (i.e. lightmapped, static, etc).
	* You can write `KDCBSPEntityParameterizer`s to make them more configurable.
4. BSP files (compiled using the relevant button in TrenchBroom) are imported as prefabs.
	* There are plenty of options for customizing the import depending on the situation.

As a key note, lightmapping and occlusion is not imported; `light` and `vis` are unused.
	* The binary-grid nature of TrenchBroom may assist in aligning everything well to make Unity occlusion play nice. Alternatively, use occlusion portals. Or both.

The ideal is that a Unity material can, at the press of a few buttons ('Generate Material PAK'?), simply _appear_ in TrenchBroom, ready for selection.

## Map Editing Tip: 'Leaks', `common/noclip`, and separation between maps

In the traditional Quake map editing workflow, you need a dummy entity in each world 'cavity' you care about (at least one) so the map compiler knows the inside and outside.

`info_player_start` is provided for this purpose (and as a measuring stick).

In addition, there mustn't be a path between the inside and the outside.

**However,** the BSP compiler will function just fine with 'leaking' maps, it'll just be unable to 'remove the void'.

So ultimately, you should pick whichever workflow you find works best for your map editing and optimization requirements.

Under some circumstances, you may want to have a map which is properly sealed _except_ at some specific entry/exit point.

This is where the `common/noclip` material comes into play. This material does not generate collision in the final product, but the BSP compiler will see it as a solid and invisible brush.

This means it:

* Still seals leaks like `common/nodraw`, so you can use it at the map's edge (which is the intended purpose)
* Still cuts faces like `common/nodraw`
* Still doesn't display anything like `common/nodraw`
* But the player can walk through it (_and immediately become 'out of bounds' relative to the current map!_)

This makes it the ideal glue between `.map` files or between `.map` and modelled content, which may be necessary to split larger maps into individual lightmaps, and is required for occlusion culling, whether trigger-based or Unity occlusion.

Technically, it carries `SOLID | CURRENT_0` contents flags (`CURRENT_0` is used as a marker by the importer to mean 'not actually solid'), and `NODRAW` face flags.

## Workspaces

KDCBSP is based around _workspaces,_ which act kind of vaguely similar to Source GameInfo files. (You'll find KDCBSP makes a lot of nods to Source and Quake. I personally consider Source to be the last truly _great_ engine. Despite it being arc-welded to the MFC abomination known as Hammer, and its single-core design, it was responsible for games that were well-optimized _even when re-rendering the scene several times over._ Unity has nearly all of Source's problems and more without any of the benefits, and the engines that followed focused on copying Unity.)

There's a default workspace at `Assets/KDCBSPGameRoot`, but you can have specific BSP files use specific workspaces, which is important for developing worlds as packages.

Workspaces set:

* The world scale (how much to divide Quake units by to get Unity units)
* Directory names for material configs and entity prefabs
* Other workspaces to find other materials and entities in, and fallback materials/entities if all else fails.

There is an implicit parent workspace at `Packages/t20kdc.vrc-bsp/Assets/builtinWorkspace.asset` which is always loaded.

This is used for things which are likely to _need_ updates, because there's no safe way to update `KDCBSPGameRoot`.

## Materials

KDCBSP finds materials relative to the paths of each included (i.e. accounting for parents/etc.) KDCBSP workspace file.

Given the input texture name `dev/32` and the default config, it looks at:

1. `KDCBSPGameRoot/materials/dev/32.asset` (for KDCBSP texture config)
2. `KDCBSPGameRoot/materials/dev/32.mat` (for Unity material; this is ignored if a texture config is found)
3. `Packages/t20kdc.vrc-bsp/Assets/textures/dev/32.asset`
4. `Packages/t20kdc.vrc-bsp/Assets/textures/dev/32.mat`

If a KDCBSP material is found and the Unity material is None, triangles will not be created.

This is one of the two useful ways to use `common/sky` (the other being a skybox material, perhaps with a custom shader with emission).

Notably, KDCBSP will also search for:

* `32.png`: TrenchBroom's version of the texture; KDCBSP calls this the 'icon'.
* `32.wal_json`: Metadata for ericw-tools; decently safe to omit _unless_ the material is special to the BSP compiler.

## Special Materials

KDCBSP has a number of special materials under the `common/` prefix.

The meanings are primarily assigned using `ericw-tools` metadata `.wal_json` files.

* If looking for `skip` / `caulk`: These are Q1 and Q3 names of `common/nodraw`.
* `common/areaportal`: Areaportals are not presently used. This is included for completeness. Ideas have been thrown around of splitting meshes by this.
* `common/clip`: Invisible collision. Distinct from `nodraw` in that it has no effects on BSP/sealing/face-cutting etc.
	* In some circumstances (i.e. if you want to control collision layers, or dynamically enable/disable the object) it may be better to use this on a brush entity.
* `common/hint`: Just manipulates BSP splitting. (We usually never care, and this is included only for completeness.)
* `common/noclip`: **Extremely special custom tool for this importer, used to integrate multiple BSPs or BSP and traditional content.** (See previous note.)
* `common/nodraw`: Invisible, but BSP compiler treats it as if it was opaque. Player can't walk through it. Seals the map.
	* Use for surfaces that always face away from the player but that would traditionally still be considered 'visible'.
	* _You do not need to `nodraw` the exterior of the map._ Assuming the map isn't 'leaking', the BSP compiler will automatically delete the exterior faces.
* `common/origin`: Controls brush-model origin! The centre of an origin brush becomes the origin of the brush entity, which is useful since this is usually i.e. a pivot.
* `common/sky`: More-or-less regular material intended to be overridden by mapper.
	* Traditionally reserved for skybox, but in Unity it's usually good enough to just not render it at all, which is the default.
* `common/trigger`: See `nodraw`. The 'can't walk through it' property is usually resolved by being part of an appropriate brush entity with trigger collision.
* `common/occluder`: This is intended for use on `func_occluder` or `func_occluder_static` geometry.
	* `VRChat/Mobile/Skybox` is used because it bakes occlusion properly, displays behind opaque geometry, and doesn't add warnings to the Android build.

## Entities

KDCBSP's entity handling is _complicated,_ particularly for brush entities.

This is because it has to split responsibility between providing settings to the map (via entity properties), providing them to the material (via the material config), and providing them to the entity itself (via entity parameterizers).

Things are much simpler for regular (non-brush) entities, which are almost entirely defined by their entity prefab.

The root of an entity prefab may have a number of `KDCBSPEntityParameterizer`-derived behaviours, which are used to 'parameterize' (i.e. setup) the entity.

* The `targetname`, if provided, names the entity.
* The `origin` key is used to place the entity.
* (The `angles` and `angle` keys are not supported right now.)

Brush entities are _much_ more complicated.

1. If `_kdcbsp_autoorigin` is `1`, then during the initial load, the entity is internally adjusted to set its origin to the centre of the bounding box of its brush model.
	* This is a pretty hacky thing and issues should be expected, since this requires the brush model to be offset again during mesh generation to fix up the chaos this causes.
2. The entity parameterizers get the opportunity to arbitrarily modify the `KDCBSPBrushEntitySettings` to their liking, alongside other chaso like changing brush convex layers
	* In practice, this is to ensure that the entity is being rendered in accordance with how the entity will actually act during gameplay.
3. There are a number of entity properties defined in `KDCBSPBrushEntitySettings`. These override anything the entity parameterizers set, which allows for fine control by mappers.
	* These aren't listed here, for now; they are already listed in the FGD.

It's also worth mentioning BSP-compiler-internal brush entities. See <https://ericw-tools.readthedocs.io/en/latest/qbsp.html#compiler-internal-bmodels>.
	* On some versions, `func_detail_illusionary` defaults to `"_mirrorinside" "1"`. _**Make sure to explicitly change it to 0, or it'll horribly break light baking!!!**_

## Overriding Functionality

KDCBSP uses a lot of abstract classes. This is because I feel you will inevitably find some reason you need to override lots of behaviour, and I'm trying to make a 'best effort' approach to allowing that.

In particular:

* Entities can be parameterized however they choose by extending `KDCBSPEntityParameterizer`.
* It is possible to override the visual mesh generation on a material-by-material basis by extending `KDCBSPAbstractMaterialConfig`. This may be useful if you're doing something fancy/weird with specially marked materials.
* Extending `KDCBSPAbstractWorkspaceConfig` allows you to define custom search logic (perhaps for texture auto-import).

