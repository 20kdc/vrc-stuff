extends Control

const PAGEHEAD_SIZE: int = 9
const SPRITE_SIZE: int = 8

var page: int = 0
var book: AtlasedBook
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

func _ready():
	reload()

func reload():
	var file = File.new()
	file.open("../drawbook/out/book.bytes", File.READ)
	var all_data = file.get_buffer(file.get_len())
	book = AtlasedBook.new(all_data)
	print("file version: 0x%x" % [book.version])
	atlases = {}
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
	var page_info: Dictionary = book.pages[page]
	var buf := book.buf
	buf.seek(page_info["sprites_ofs"])
	var page_sprites: int = page_info["sprites_count"]
	var atlas_id: int = page_info["atlas_id"]
	var page_size: Vector2 = page_info["size"]
	for v in range(page_sprites):
		# read sprite details
		var shape_id = buf.get_16() & 0xFFFF
		var tlx = float(((buf.get_16() + 0x8000) & 0xFFFF) - 0x8000) / 32767.0
		var tly = float(((buf.get_16() + 0x8000) & 0xFFFF) - 0x8000) / 32767.0
		var tl = Vector2(tlx, tly) * page_size
		var colour_index = buf.get_16() & 0xFFFFF
		var mod = book.palette[colour_index]
		# get shape
		var shape_info: Array = book.atlases[atlas_id][shape_id]
		var src: Rect2 = shape_info[0]
		var size: Vector2 = shape_info[1]
		var atlas = get_atlas(atlas_id)
		src = Rect2(src.position * atlas.get_size(), src.size * atlas.get_size())
		# target region
		var targ := Rect2(tl, size)
		# actual draw code
		if drawpass == 0:
			if debug:
				draw_rect(targ, Color.green, false, 2)
			draw_texture_rect_region(atlas, targ, src, mod)
		if drawpass == 1:
			draw_rect(targ, Color.red, false)
