# Narbacular Drop map compiler for testing

This is a map compiler (replacement for `csg.exe`) for the game Narbacular Drop by Nuclear Monkey Software (the team which later joined Valve, leading to Portal and so forth).

It has a number of convenience features that mainly serve to test the underlying backend.

In particular:

* The compiler handles 'TrenchBroom export' logic internally.
* Worldspawn property: `_kvbsp`: Disables `csg.exe`-compatibility.
	* Worldspawn brushes are automatically converted to `collidable_geometry`, and the world is partitioned based on the location of the `player_respawn` entity.
		* `sfx_type` (and thus portalability) is set based on `_type:TEXTURE` worldspawn keys, i.e. `_type:dirt_floor1`.
	* Partitioning holes may be filled with a material called `noclip`. Same deal as in KDCVRCBSP itself; chops, seals leaks, no visual effect.
	* `func_group` entities (that is, which aren't created by TrenchBroom; those are _always_ cleaned even in csg.exe compatibility mode) are auto-merged into worldspawn.
* Entity property: `_lightslice`: Using this property, a bunch of axis-aligned lines are created to allow vertex lighting to work properly.
	* These are offset by half of the internal epsilon. This prevents the offset from causing weird geometry, but prevents the player/boxes/etc. from falling through the floor due to alignment.

## Quirks

This map compiler project also acts as documentation of interesting Narbacular Drop quirks, because I guess I'm easily distracted by tangents.

* `twodoor.map`: Two `level_end` entities can exist and act as a choice, but this probably can only happen for the last map in a set.
* `realtest.map`: This shows a lot of quirks around a single `level_end` entity. In particular:
	* Walking past the extent of _any_ `level_end` entity (to the Y+ direction) must never happen unless the door has opened.
		* It will even break respawning.
	* Respawning after the door has been opened once leaves the target level in a 'pre-loaded' state, collidable but not active or visible.
	* Walking past the extent of that entity after opening the door and then respawning unloads the last level while not completing loading your new level.
	* Going _backwards_ after entering a new level is safe enough.
	* A portal with no backing surface (i.e. that is not being 'carved into') cannot be entered (though it can be exited).
* The level object function table is`(?, setup, ?, update, whentargetted, ?, ?, ?)`.
* Counters work via repeated update and so are subtly affected by update order (entities that update after them will have a frame of delay).
	* The threshold check is at `424bdc`; changing it to `75`/`jne` would have created an equality check, allowing for a full set of logic gates. And also this doesn't break the original campaign, though it could subtly break one which used counters as an unresettable switch. (The only thing worth driving this way in the original executable, though, is lava.)
* Several entities have logic indicating they 'should' have a `targetname`, but they don't actually look for it in their description. The most obvious victim of this is the slightly improperly documented `PlayerRespawn` target.
	* The entities that check for `targetname` are: `area_trigger`, `func_door`, `func_lava`, `ambient_sound`, `boulder_spawn`, `level_end`, `counter`, `game_text`, the light variants, and `camera`.
	* Hardcoded targetnames seem to be:
		* `LevelTitle`: Impy respawner (`impy`), pre-placed portals (`blue_portal`, `red_portal`)
		* `PlayerRespawn`: `player_respawn`, Impy instance
* `func_door` is _frustratingly,_ painfully close to being a thing. Notably, it seems like it was designed as a point entity.
* The documented behaviour of lava is to raise 32 units on target. But 'target' from a counter tends to be a continuous process more often than not.
* `preloadnextlevel` is kind of dangerous. Try running it twice, and then opening the end door. Oops, the 'next level' slot is now level 3, the current level slot is now level 2, which hasn't actually activated yet, and if you respawn, you'll end up moving past level 2's exit door, softlocking yourself.
	* The command can 'kind of' escape softlocks caused by travelling beyond the exit door. The problem is this has a nasty habit of causing multiple separate exit doors to end up opening from different level instances. And for walls to start going missing...
* The on-load console command is run when the level activates. `preloadnextlevel` (which loads the level in a deactivated state) doesn't trigger it, but opening the exit door (when the threshold hasn't been crossed) does.