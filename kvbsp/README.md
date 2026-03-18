# Quake 2 BSP Import

This Quake 2 BSP importer allows creating static world geometry with, i.e. TrenchBroom and a more recent `ericw-tools` (such as one of the 2.0.0 alpha releases from <https://github.com/ericwa/ericw-tools/releases>).

The basic idea is that:

1. Copy package's `TrenchBroom~/KVToolsTB/CompilationProfiles-example.cfg` to `TrenchBroom~/KVToolsTB/CompilationProfiles.cfg`; you'll need to adjust this later.
2. Symlink `.TrenchBroom/games/KVToolsTB` to the `TrenchBroom~/KVToolsTB` directory of the repository
	* Or, if you absolutely must avoid symlinks, copy from `TrenchBroom~/KVToolsTB` to `.TrenchBroom/games/KVToolsTB`
	* For different Unity projects, it may be wise to use separate 'games'.
3. TrenchBroom will now show the 'game' `KVToolsTB` on startup.
4. **TODO figure out clean setup workflow**
5. `qroot/baseq2` (where `qroot` is a 'global workspace'; TrenchBroom doesn't handle multi-game arrangements well) to the `Assets/baseq2` directory of the repository
6. Each individual project is a 'mod' you symlink from `qroot`

Note the `KVToolsTB/CompilationProfiles-example.cfg` file. This is a hastily kludged compilation profile; adjust as you need/wish.

With `kvtools`, the resulting `.bsp` file can be directly imported in Unity. This requires a "KDCBSP Workspace Config" (creatable as an asset) to map materials and set world scale.

Some particular notes:

* You need a dummy entity in each world 'cavity' you care about (at least one) so the map compiler knows the inside and outside.
	* `info_player_start` is provided for this purpose.
* Lightmapping is still handled by Unity, so don't bother running the Q2BSP `light`.
* **Entities are not really supported, but the BSP compiler may have 'built-in' entities. See <https://ericw-tools.readthedocs.io/en/latest/qbsp.html#compiler-internal-bmodels>.**
* On some versions, `func_detail_illusionary` defaults to `"_mirrorinside" "1"`. _**Make sure to explicitly change it to 0, or it'll horribly break light baking!!!**_
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

