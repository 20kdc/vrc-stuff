# 'KVBSP ECL BSP compiler' material roles

These material roles are essentially presets for flags to pass to the 'KVBSP ECL' compiler.

They are:

* `areaportal`: Areaportal face.
	* Other faces are expected to be something like `hint`.
* `clip`: Like `detail`, but removes render face, doesn't interfere with T-junction processing, and also can't be chopped (since this is invisible anyway).
* `detail`: Forces the entire brush to be detail. Doesn't split the BSP and can't delete other faces (which would open up holes).
	* This role is what you are expected to use for transparent objects, if you have them as standard brushes and you want them to get chopped.
	* Once other stuff is stabilized, a future update may make detail able to chop other detail by guaranteeing it always loses to split faces during the chop stage.
* `hint`: Marks the brush illusionary, won't chop anything, won't delete leaves, doesn't render to anything or affect T-junction processing, but still splits the BSP.
	* Use this when you're using a texture on another face that requires the BSP to be split for something.
* `noclip`: Illusionary, no collide/render geometry, but _otherwise_ still a solid face, chops brushes, contributes to T-junction processing.
	* This is intended for inter-map transitions.
* `nodraw`: Solid, collidable, chopping geometry that doesn't render and has no effect on T-junctions.
* `origin`: Used for the origin brush. Is deleted immediately after AABB calculation, never to be seen again.

These material roles are mapped via 'material role' resources to sets of flags that control the internal behaviour of the compiler.

Note that misuse of material roles (or materials defined with them) can result in odd behaviour, such as hard-to-explain leaks.
