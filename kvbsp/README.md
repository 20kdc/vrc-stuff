# Quake BSP Import

This Quake 1/2/3/GoldSrc BSP importer allows creating static world geometry with, i.e. TrenchBroom and a more recent `ericw-tools` (such as one of the 2.0.0 alpha releases from <https://github.com/ericwa/ericw-tools/releases>).

## Quickstart

The basic idea is that:

1. Install the package and look at `Setup.asset` in Unity.
	* You can choose TrenchBroom or NetRadiant-custom. The setup will add the necessary configuration.
2. Add Unity materials to the `KDCBSPGameRoot/materials` directory.
	* Be sure to press the "Update Quake VFS" button on your workspace when you change materials! This does some setup so that your map editor can see your materials, so you may need to restart it.
3. Add prefabs to the `KDCBSPGameRoot/progs` directory to add new entity types.
	* You can implement editor-only behaviours (subclass `KDCBSPEntity`) or Udon behaviours (implement `IKDCBSPEntity` and use conditional compilation) which get to customize the import.
4. BSP files (compiled using the relevant button in the map editor) are imported as prefabs, which you can then use in your VRChat worlds.
	* There are plenty of options for customizing the import depending on the situation.
	* Due to fundamental differences in format and capabilities, lightmapping and occlusion is not imported; `light` and `vis` are unused. The prefabs are compatible with Unity's lightmapper.

## Workspaces

KDCBSP is based around _workspaces,_ which act kind of vaguely similar to Source GameInfo files.

Workspaces set:

* The world scale (how much to divide Quake units by to get Unity units)
* Directory names for material configs and entity prefabs
* Other workspaces to find other materials and entities in, and fallback materials/entities if all else fails.

There's a default workspace at `Assets/KDCBSPGameRoot`, but you can have specific BSP files use specific workspaces.

There are two workflows you can use here:

1. Use workspaces to isolate individual sub-projects. If you use a one-Unity-project-per-VRChat-project approach, you get this 'by default'.
2. Have the game root workspace + one workspace per participating Unity package (added to the game root as parents), keep material and entity type names unique between packages whenever possible. This prevents needing to switch game roots when switching between packages in the same Unity project, and is the workflow used for kvbsp development.

Finally, there is an implicit parent workspace at `Packages/t20kdc.vrc-bsp/Assets/builtinWorkspace.asset` which is always loaded; this is used for things that are relatively integral to kvbsp itself and definitely need to be updated with it.

## Materials

kvbsp's handling of materials is intended to be flexible.

There are two built-in kinds of kvbsp material.

* 'Standard' kvbsp materials. These are streamlined for 'main' materials which don't need explicit BSP compiler customization, just reasonably straightforward Unity things.
	* There are preset 'compile modes' (presently just normal and transparent) to adjust how the BSP compiler behaves with the material.
* 'Tool' materials. These are streamlined for 'tool' materials, which are _always nodraw at import time,_ though it may sometimes be of use to convince the BSP compiler they are actually drawn for other uses, such as concave collision.

While you generally shouldn't make tool materials, both kinds of material config can be created via the 'create asset menu'. Alternatively, if a Unity material file exists (`.mat`) without a corresponding kvbsp metadata asset (`.asset`), kvbsp will automatically internally create a standard kvbsp material to wrap it (the file won't be saved).

The 'Create Quake VFS' button has to expose all of these materials in a form acceptable to the map editor but also correctly scaled (called the 'icon' in kvbsp). This involves the creation of Quake 3 shader files and potentially image resizing. 

If it can't figure that out, it may give a 'no icon' image to the map editor. In this case, or if it gets something wrong, you may want to add a PNG file (named the same as the material) to override the icon.

Given the input texture name `dev/32` and the default config, it looks at:

1. `KDCBSPGameRoot/materials/dev/32.asset` (for KDCBSP texture config)
2. `KDCBSPGameRoot/materials/dev/32.mat` (for Unity material; this is ignored if a texture config is found)
3. `Packages/t20kdc.vrc-bsp/Assets/textures/dev/32.asset`
4. `Packages/t20kdc.vrc-bsp/Assets/textures/dev/32.mat`

If a KDCBSP material is found and the Unity material is None, triangles will not be created.

This is one of the two useful ways to use `common/sky` (the other being a skybox material, perhaps with a custom shader with emission).

Notably, next to the found material, KDCBSP will also search for `32.png` (the 'icon override' as mentioned earlier).

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

## Special Materials

KDCBSP has a number of special materials under the `common/` prefix.

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

This is because it has to split responsibility between providing settings to the map (via entity properties), providing them to the material (via the material config), and providing them to the entity itself (via the entity implementation).

The root of an entity prefab may have a component which implements `IKDCBSPEntity`. This component defines how the entity is to be compiled (after having been instantiated from the prefab).

How this works out in practice depends on what you are trying to do:

* Entity component with solely import-time code: Extend `KDCBSPEntity`. This will default to compiling brushes for you and can be customized by overriding methods.
* Point entity Udon behaviour: Implement `IKDCBSPEntity`, gated behind `#if !COMPILER_UDONSHARP` as interface implementations are not allowed in UdonSharp.
* Brush entity Udon behaviour: Copy `KDCBSPEntity`'s code into your `IKDCBSPEntity` implementation and modify to taste.

The following are built-in and apply to all entities.

* The `targetname`, if provided, names the entity.
* The `origin` key is used to place the entity.
* (The `angles` and `angle` keys are not supported right now, but they would rotate the entity.)
* If `_kdcbsp_autoorigin` is `1` and a brush model exists, the entity is internally adjusted to set its origin to the centre of the bounding box of its brush model.
	* This is pretty hacky, but should be reliable.
* The entity component gets the opportunity to arbitrarily modify the `KDCBSPBrushEntitySettings` to its liking, alongside other chaos like changing brush convex layers.
	* In practice, this is to ensure that the entity is being rendered in accordance with how the entity will actually act during gameplay.
* There are a number of entity properties defined in `KDCBSPBrushEntitySettings`. These usually override anything the entity component sets, which allows for fine control by mappers.
	* These aren't listed here, for now; they are already listed in the FGD.

It's also worth mentioning BSP-compiler-internal brush entities. See <https://ericw-tools.readthedocs.io/en/latest/qbsp.html#compiler-internal-bmodels>.
	* On some versions, `func_detail_illusionary` defaults to `"_mirrorinside" "1"`. _**Make sure to explicitly change it to 0, or it'll horribly break light baking!!!**_

## Overriding Functionality

KDCBSP uses a lot of abstract classes. This is because I feel you will inevitably find some reason you need to override lots of behaviour, and I'm trying to make a 'best effort' approach to allowing that.

In particular:

* After instantiation, entities can be compiled mostly however they choose using a component implementing `IKDCBSPEntity`.
* It is possible to override the visual mesh generation on a material-by-material basis by extending `KDCBSPAbstractMaterialConfig`. This may be useful if you're doing something fancy/weird with specially marked materials.
* Extending `KDCBSPAbstractWorkspaceConfig` allows you to define custom search logic (perhaps for texture auto-import).

## Occluder Geometry Generation

Occluder geometry generation is a mechanism to automate the creation of _good_ occlusion geometry, which doesn't cause unexpected glitches when too close to a wall. This geometry is intentionally conservative about occlusion, focusing more on correctness than on how much it can cull.

It generates `EditorOnly` meshes which are marked as static occluders. It generates these away from the walls using basically the same principle Quake used for clipnodes in reverse so that Unity doesn't inevitably bug out because the occluder cells are too big or something.
