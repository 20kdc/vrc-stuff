extends Control

const PAGEHEAD_SIZE: int = 9
const SPRITE_SIZE: int = 8

var page: int = 0
var book: AtlasedBook
var page_count: int
var shapes: Dictionary
var debug: bool = false

func _ready():
	reload()

func reload():
	book = AtlasedBook.new("../drawbook/out/book.bytes", false)
	print("file version: 0x%x" % [book.version])
	shapes = {}

func _gui_input(event):
	if event is InputEventKey:
		var key := event as InputEventKey
		if key.pressed:
			if key.scancode == KEY_R:
				reload()
				update()
			if key.scancode == KEY_LEFT:
				page -= 1
				update()
			if key.scancode == KEY_RIGHT:
				page += 1
				update()
			if key.scancode == KEY_F:
				if material == preload("pagerendermaterial.tres"):
					material = null
				else:
					material = preload("pagerendermaterial.tres")
				update()
			if key.scancode == KEY_M:
				debug = !debug
				update()
		accept_event()

func _draw():
	if page >= page_count:
		page = page_count - 1
	if page < 0:
		page = 0
	if page >= book.page_count:
		return
	_drawpass(0)
	if debug:
		_drawpass(1)

func _drawpass(drawpass: int):
	var atlas_id := book.page_atlases[page]
	var page_size := book.page_sizes[page]
	var buf := StreamPeerBuffer.new()
	buf.big_endian = false
	buf.data_array = book.page_sprites[page]
	for _v in range(buf.get_size() / 8):
		# read sprite details
		var shape_id = buf.get_16() & 0xFFFF
		var tlx = float(((buf.get_16() + 0x8000) & 0xFFFF) - 0x8000) / 32767.0
		var tly = float(((buf.get_16() + 0x8000) & 0xFFFF) - 0x8000) / 32767.0
		var tl = Vector2(tlx, tly) * page_size
		var colour_index = buf.get_16() & 0xFFFFF
		var mod = book.palette[colour_index]
		# get shape
		var shape_info: Array = book.atlas_shapes[atlas_id][shape_id]
		var src: Rect2 = shape_info[0]
		var size: Vector2 = shape_info[1]
		var atlas = book.atlas_textures[atlas_id]
		src = Rect2(src.position, src.size)
		# target region
		var targ := Rect2(tl, size)
		# actual draw code
		if drawpass == 0:
			if debug:
				draw_rect(targ, Color.green, false, 2)
			draw_texture_rect_region(atlas, targ, src, mod)
		if drawpass == 1:
			draw_rect(targ, Color.red, false)
