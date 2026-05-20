class_name AtlasedBookPageAccess
extends Reference

var book: AtlasedBook

var atlas_id: int
var page_size: Vector2
var sprite_ofs: int
var sprite_count: int

var sprite_shape_id: int
var sprite_pos: Vector2
var sprite_colour: Color

func _init(bk: AtlasedBook, page: int):
	book = bk
	if bk == null or page < 0 or page >= len(bk.page_sizes):
		print("rejected. " + str(bk) + " " + str(page))
		# out of range
		atlas_id = 0
		page_size = Vector2(0, 0)
		sprite_count = 0
	else:
		# in-range
		atlas_id = bk.page_atlases[page]
		page_size = bk.page_sizes[page]
		sprite_ofs = bk.page_sprites_ofs[page]
		sprite_count = bk.page_sprites_count[page]

func read_sprite(sprite: int):
	var buf := book.buf
	buf.seek(sprite_ofs + (sprite * 8))
	# read sprite details
	sprite_shape_id = buf.get_16() & 0xFFFF
	var tlx = float(((buf.get_16() + 0x8000) & 0xFFFF) - 0x8000) / 32767.0
	var tly = float(((buf.get_16() + 0x8000) & 0xFFFF) - 0x8000) / 32767.0
	sprite_pos = Vector2(tlx, tly) * page_size
	sprite_colour = book.palette[buf.get_16() & 0xFFFF]
