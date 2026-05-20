# Parsed/renderable book.
class_name AtlasedBook
extends Reference

# The representation here is limited by the fact that Godot 3.x has an eternally unsolved bug.
# That eternally unsolved bug limits the number of Pool*Array allocations.
# For this reason, we make a point NOT to use Array whenever possible.

var buf: StreamPeerBuffer

# array of arrays of shape arrays [src: Rect2, size: Vector2]
# source is expressed as 0-1 UVs that have already been flipped
var atlas_shapes: Array = []
var atlas_textures: Array = []
# array of Color
var palette: Array = []

var page_atlases: Array = []
var page_sizes: Array = []
var page_sprites_ofs: Array = []
var page_sprites_count: Array = []

var version: int = 0
var page_count: int = 0
