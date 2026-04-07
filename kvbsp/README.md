# Quake 2 BSP Import

This Quake 2 BSP importer allows creating static world geometry with, i.e. TrenchBroom and a more recent `ericw-tools` (such as one of the 2.0.0 alpha releases from <https://github.com/ericwa/ericw-tools/releases>).

The basic idea is that:

1. Install the package and look at `Setup.asset` in Unity, which automates the following:
	1. Install `TrenchBroom~/KVToolsTB` to inside TrenchBroom's user directory (Linux: `$HOME/.TrenchBroom/games/KVToolsTB`)
	2. Adjust `CompilationProfiles.cfg` to point at ericw-tools QBSP.
	3. Copy `TrenchBroom~/KDCBSPGameRoot` to `Assets/` of your Unity project.
		* Note that your game root may _actually_ be anywhere; KDCBSP intentionally does not care about this. This style is used for easy onboarding, but more experienced users may find it prudent to treat different projects as 'mods' on a single game root that exists outside of any Unity project, using symlinking.
			* Regardless of this, `Assets/KDCBSPGameRoot/DefaultWorkspaceConfig.asset` is the hardcoded default workspace config.
2. Add images to the game root textures tree to add materials in TrenchBroom
3. Add materials to the same tree to add materials in Unity.
	* More precise configuration can be added by creating KDCBSP material config files of the same name.
4. BSP files (compiled using the relevant button in TrenchBroom) are imported as prefabs.
	* There are plenty of options for customizing the import depending on the situation.

## Detailed Notes

* KDCBSP finds materials and entity prefabs relative to the paths of each included (i.e. accounting for parents/etc.) KDCBSP workspace file.
	* Given the input texture name `dev/32` and the default config, it looks at:
		1. `KDCBSPGameRoot/baseq2/textures/dev/32.asset` (for KDCBSP texture config)
		2. `KDCBSPGameRoot/baseq2/textures/dev/32.mat` (for Unity material; this is ignored if a texture config is found)
* You need a dummy entity in each world 'cavity' you care about (at least one) so the map compiler knows the inside and outside.
	* `info_player_start` is provided for this purpose.
* Lightmapping and occlusion is not imported; `light` and `vis` are unused.
	* The binary-grid nature of TrenchBroom may assist in aligning everything well to make Unity occlusion play nice. Alternatively, use occlusion portals. Or both.
* Brush entities come in various flavours!
	* BSP-compiler-internal: See <https://ericw-tools.readthedocs.io/en/latest/qbsp.html#compiler-internal-bmodels>.
		* On some versions, `func_detail_illusionary` defaults to `"_mirrorinside" "1"`. _**Make sure to explicitly change it to 0, or it'll horribly break light baking!!!**_
	* Prefabs: So there's a whole _process_ here.
* If the Unity material is None, triangles will not be created. This is one of the two useful ways to use `common/sky` (the other being a skybox material, perhaps with a custom shader with emission).
* Special materials (note: The meanings are primarily assigned using `ericw-tools` metadata `.wal_json` files):
	* If looking for `skip` / `caulk`: These are Q1 and Q3 names of `common/nodraw`.
	* `common/areaportal`: Areaportals are not presently used. This is included for completeness. Ideas have been thrown around of splitting meshes by this.
	* `common/clip`: Invisible collision. Distinct from `nodraw` in that it has no effects on BSP/sealing/face-cutting etc.
	* `common/hint`: Just manipulates BSP splitting. (We usually never care, and this is included only for completeness.)
	* `common/noclip`: **Extremely special custom tool for this importer, used to integrate multiple BSPs or BSP and traditional content.** (See below.)
	* `common/nodraw`: Invisible, but BSP compiler treats it as if it was opaque. Player can't walk through it. Seals the map.
		* Use for surfaces that always face away from the player but that would traditionally still be considered 'visible'.
		* _You do not need to `nodraw` the exterior of the map._ Assuming the map isn't 'leaking', the BSP compiler will automatically delete the exterior faces.
	* `common/origin`: Controls brush-model origin. Included only for completeness.
	* `common/sky`: More-or-less regular material intended to be setup by mapper. Traditionally reserved for skybox.
	* `common/trigger`: See `nodraw`. The 'can't walk through it' property is usually resolved by being part of an appropriate brush entity, but these aren't supported. Included only for completeness.
	* `common/occluder`: This is intended for use on `func_occluder` or `func_occluder_static` geometry.
		* `VRChat/Mobile/Skybox` is used because it:
			1. Bakes occlusion properly.
			2. Displays behind opaque geometry (stays out of your way)
			3. Is a Mobile material and thus doesn't add warnings to the Android build
* While most materials act as per 'usual BSP standards', the `common/noclip` material is special. It's `common/nodraw` **without collision.**
	* This means it:
		* Still seals leaks like `common/nodraw`, so you can use it at the map's edge (which is the intended purpose)
		* Still cuts faces like `common/nodraw`
		* Still doesn't display anything like `common/nodraw`
		* But the player can walk through it (_and immediately become 'out of bounds' relative to the current map!_)
	* This is the ideal glue between `.map` files or between `.map` and modelled content.
	* This separation may be necessary to split larger maps into individual lightmaps.
	* This separation is required for occlusion culling, whether trigger-based or Unity occlusion.
	* Technically, it carries `SOLID | CURRENT_0` contents flags (`CURRENT_0` is used as a marker by the importer to mean 'not actually solid'), and `NODRAW` face flags.

## Brush Entity Compilation Settings

`KDCBSPImporter.CreateEntity` is essentially 'everything of importance after loading the file'.

Everything in the importer is expressed as creating an entity, including the base world geometry (the `worldspawn` entity).

'Point' entities are mainly user-controlled; see the `KDCBSPEntityParameterizer` interface if you want to mess with that.

A key point in all this is `KDCBSPBrushEntitySettings`. This controls a _lot_ of details of brush entity compilation, so it's pretty important to know what gets to alter it.

1. First, the worldspawn and brush entity templates are taken from the import settings.
2. They are either given to KDCBSPEntityParameterizer (if one exists) to pick and edit, or one is chosen based on which is obviously correct (if not).
3. Then, entity overrides (`kdcbsp_` keys other than `kdcbsp_autoorigin`) are executed.


