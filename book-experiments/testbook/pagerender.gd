extends Node2D

const PAGEHEAD_SIZE: int = 8
const SPRITE_SIZE: int = 8

var file = File.new()
var page_lump: int = 1
var lump_count: int
var data: PoolByteArray
var shapes: Dictionary
var debug: bool = false

func get_shape(i: int) -> Dictionary:
	if shapes.has(i):
		return shapes[i]

	# grab info from shapes lump
	file.seek(lump_pos(0) + (i * 17))
	var atlas_id = file.get_8()
	var tlx = float(file.get_16() & 0xFFFF) / 65535.0
	var tly = float(file.get_16() & 0xFFFF) / 65535.0
	var brx = float(file.get_16() & 0xFFFF) / 65535.0
	var bry = float(file.get_16() & 0xFFFF) / 65535.0
	var w = file.get_float()
	var h = file.get_float()

	var img := Image.new()
	if img.load("../drawbook/debug/s" + str(i) + ".sdf.png") != OK:
		return {}
	var tex := ImageTexture.new()
	tex.create_from_image(img, Texture.FLAG_FILTER)
	var res := {}
	res["tex"] = tex
	res["src"] = Rect2(0, 0, img.get_width(), img.get_height())
	res["size"] = Vector2(w, h)
	shapes[i] = res
	return res

func lump_pos(i: int) -> int:
	file.seek(4 + (i * 8))
	return file.get_32()
func lump_size(i: int) -> int:
	file.seek(8 + (i * 8))
	return file.get_32()

func _ready():
	reload()

func reload():
	file.open("../drawbook/book.bin", File.READ)
	lump_count = file.get_32()
	shapes = {}

func _input(event):
	if event is InputEventKey:
		var key := event as InputEventKey
		if key.pressed:
			if key.scancode == KEY_R:
				reload()
				update()
			if key.scancode == KEY_LEFT:
				page_lump -= 1
				update()
			if key.scancode == KEY_RIGHT:
				page_lump += 1
				update()
			if key.scancode == KEY_M:
				debug = !debug
				update()

func _draw():
	if page_lump >= lump_count:
		page_lump = lump_count - 1
	if page_lump < 1:
		page_lump = 1
	_drawpass(0)
	if debug:
		_drawpass(1)

func _drawpass(drawpass: int):
	var page_pos := lump_pos(page_lump)
	var page_sprites := (lump_size(page_lump) - PAGEHEAD_SIZE) / SPRITE_SIZE
	# read page header
	file.seek(page_pos)
	var page_w: float = file.get_float()
	var page_h: float = file.get_float()
	var page_size := Vector2(page_w, page_h)
	page_pos += PAGEHEAD_SIZE
	for v in range(page_sprites):
		file.seek(page_pos)
		page_pos += SPRITE_SIZE
		# read sprite details
		var shape = file.get_16() & 0xFFFF
		var tlx = float(((file.get_16() + 0x8000) & 0xFFFF) - 0x8000) / 32767.0
		var tly = float(((file.get_16() + 0x8000) & 0xFFFF) - 0x8000) / 32767.0
		var tl = Vector2(tlx, tly) * page_size
		var rgb565 = file.get_16()
		var cr = float((rgb565 >> 11) & 0x1F) / 0x1F
		var cg = float((rgb565 >> 5) & 0x3F) / 0x3F
		var cb = float((rgb565 >> 0) & 0x1F) / 0x1F
		var mod = Color(cr, cg, cb)
		# get shape (seeks)
		var shp = get_shape(shape)
		# figure out size even if get_shape fails somehow
		var size := Vector2(1, 1)
		if shp.has("size"):
			size = shp["size"]
		# target region
		var targ := Rect2(tl, size)
		# actual draw code
		if drawpass == 0:
			if debug:
				draw_rect(targ, Color.green, false, 2)
			if shp.has("tex"):
				draw_texture_rect_region(shp["tex"], targ, shp["src"], mod)
		if drawpass == 1:
			draw_rect(targ, Color.red, false)
