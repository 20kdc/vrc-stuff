# Parsed/renderable book.
class_name AtlasedBook
extends Reference

# array of arrays of shape arrays [src: Rect2, size: Vector2]
# source is expressed as 0-1 UVs that have already been flipped
var atlas_shapes: Array = []
var atlas_textures: Array = []
# array of Color
var palette: PoolColorArray = PoolColorArray()
# array of page descriptors {
#  atlas_id: int,
#  size: Vector2,
#  sprites: PoolByteArray
# }
var pages: Array = []

var page_atlases: PoolIntArray = PoolIntArray()
var page_sizes: PoolVector2Array = PoolVector2Array()
var page_sprites: Array = []

var version: int = 0
var page_count: int = 0

func palette_add(c: Color):
	palette.push_back(c)

func page_add(atlas: int, size: Vector2, sprites: PoolByteArray):
	page_atlases.push_back(atlas)
	page_sizes.push_back(size)
	page_sprites.push_back(sprites)
