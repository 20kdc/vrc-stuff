# Props On Rails

'Props On Rails' is an attempt to implement synchronized pickups which move along rails.

There are all sorts of theoretical fun uses for it, but to be clear, it's not designed to be 100% reliable as a game mechanic, or perfectly interpolated or whatever. It's designed as a flexible way to implement silly gimmicky interactions.

## "Rail Public API"

The API rails expose consists of the properties:

* `railApi_point`: `Vector3`, World-space position.
* `railApi_lerp`: `float` from 0 to 1.
* `railApi_rotation`: `Quaternion`, World-space rotation.
* `railApi_neighbours`: `Transform[]`, refers to other rails, do not modify.

And these custom events:

* `_railApi_GetClosestPoint`: Sets `railApi_lerp` to a value from 0 to 1 based on `railApi_point`, then calls `_railApi_GetPoint`.
* `_railApi_GetPoint`: Sets `railApi_point`, `railApi_rotation` and `railApi_neighbours` based on `railApi_lerp`.

## Conceptual Workings

An 'on-rails entity' maintains two pieces of state:

* Its 'parent rail' (not a real parent)
* Its 'rail lerp'

When the entity needs to keep itself on the rail, it sends its current position to `railApi_point` of its parent rail and calls `_railApi_GetClosestPoint`. The resulting `railApi_point` and `railApi_rotation` become the 'current best case', with the distance between `railApi_point` and the original position being stored along with the rail.

The `railApi_neighbours` are then checked. It is possible neighbours may be invalid objects; these are skipped. For each valid neighbour, `railApi_point` is set as before and `_railApi_GetClosestPoint` is called. If the neighbour's result `railApi_point` is closer, it overwrites the 'current best case'.

The object is teleported to the position and rotation of the winning case.

To implement synchronization, a bit of a mess had to be performed. There is the synchronized rail follower entity, the position of which lives directly on the rail, and the desynchronized handle. The desynchronized handle's job is to both be the pickup the player actually uses and to implement what amounts to a very carefully choreographed parent constraint.