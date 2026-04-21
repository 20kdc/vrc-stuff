extends Node2D

const PAGEHEAD_SIZE: int = 9
const SPRITE_SIZE: int = 8

var file = File.new()
var page: int = 0
var atlas_count: int
var page_count: int
var atlases: Dictionary
var shapes: Dictionary
var debug: bool = false

func get_atlas(i: int) -> ImageTexture:
	if atlases.has(i):
		return atlases[i]
	var img := Image.new()
	if img.load("../drawbook/out/atlas." + str(i) + ".png") != OK:
		return null
	var atlas = ImageTexture.new()
	atlas.create_from_image(img, Texture.FLAG_FILTER)
	atlases[i] = atlas
	return atlas

func get_shape(i: int) -> Dictionary:
	if shapes.has(i):
		return shapes[i]

	var atlas_id = i >> 16
	var shape_id = i & 0xFFFF

	# grab info from atlas's lump
	file.seek(lump_pos(2 + atlas_id) + (shape_id * 16))
	var tlx = float(file.get_16() & 0xFFFF) / 65535.0
	var tly = float(file.get_16() & 0xFFFF) / 65535.0
	var brx = float(file.get_16() & 0xFFFF) / 65535.0
	var bry = float(file.get_16() & 0xFFFF) / 65535.0
	var w = file.get_float()
	var h = file.get_float()

	var atlas = get_atlas(atlas_id)

	var tl = Vector2(tlx, 1.0 - tly) * atlas.get_size()
	var br = Vector2(brx, 1.0 - bry) * atlas.get_size()

	var res := {}
	res["tex"] = atlas
	res["src"] = Rect2(tl, br - tl)
	res["size"] = Vector2(w, h)
	shapes[i] = res
	return res

func lump_pos(i: int) -> int:
	file.seek(8 + (i * 8))
	return file.get_32()
func lump_size(i: int) -> int:
	file.seek(12 + (i * 8))
	return file.get_32()

func _ready():
	reload()

func reload():
	file.open("../drawbook/out/book.bytes", File.READ)
	atlas_count = file.get_16()
	print("file version: 0x%x" % [file.get_16()])
	page_count = file.get_32()
	atlases = {}
	shapes = {}

func _input(event):
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
			if key.scancode == KEY_M:
				debug = !debug
				update()

func _draw():
	if page >= page_count:
		page = page_count - 1
	if page < 0:
		page = 0
	_drawpass(0)
	if debug:
		_drawpass(1)

func _drawpass(drawpass: int):
	var page_lump := 2 + atlas_count + page
	var page_pos := lump_pos(page_lump)
	var page_sprites := (lump_size(page_lump) - PAGEHEAD_SIZE) / SPRITE_SIZE
	# read page header
	file.seek(page_pos)
	var atlas_id: int = (file.get_8() & 0xFF) << 16
	var page_w: float = file.get_float()
	var page_h: float = file.get_float()
	var page_size := Vector2(page_w, page_h)
	page_pos += PAGEHEAD_SIZE
	for v in range(page_sprites):
		file.seek(page_pos)
		page_pos += SPRITE_SIZE
		# read sprite details
		var shape = atlas_id | (file.get_16() & 0xFFFF)
		var tlx = float(((file.get_16() + 0x8000) & 0xFFFF) - 0x8000) / 32767.0
		var tly = float(((file.get_16() + 0x8000) & 0xFFFF) - 0x8000) / 32767.0
		var tl = Vector2(tlx, tly) * page_size
		var colour_index = file.get_16()
		# palette lookup
		file.seek(lump_pos(1) + (colour_index * 4))
		var cr = file.get_8()
		var cg = file.get_8()
		var cb = file.get_8()
		var ca = file.get_8()
		var mod = Color8(cr, cg, cb, ca)
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
