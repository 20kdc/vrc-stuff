# Parsed/renderable book.
class_name AtlasedBook
extends Reference

const LUMP_METADATA = 0
const LUMP_PALETTE = 1
const LUMP_ATLAS0 = 2

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

func _init(path: String, web: bool):
	var f := StreamPeerBuffer.new()
	f.big_endian = false

	var web_image: Image = null
	if web:
		web_image = Image.new()
		web_image.load(path)
		f.data_array = web_image.data["data"]
	else:
		var file := File.new()
		file.open(path, File.READ)
		f.data_array = file.get_buffer(file.get_len())
		file.close()

	var path_base_dir := path.get_base_dir()

	var atlas_count = f.get_16() & 0xFFFF
	version = f.get_16() & 0xFFFF
	page_count = f.get_32() & 0xFFFFFFFF
	# lumps
	var lump_ofs := []
	var lump_len := []
	for _i in range(LUMP_ATLAS0 + atlas_count + page_count):
		lump_ofs.push_back(f.get_32() & 0xFFFFFFFF)
		lump_len.push_back(f.get_32() & 0xFFFFFFFF)
	# read palette
	var palette_ofs = lump_ofs[LUMP_PALETTE]
	var palette_count = lump_len[LUMP_PALETTE] / 4
	f.seek(palette_ofs)
	for _i in range(palette_count):
		var cr := f.get_8() & 0xFF
		var cg := f.get_8() & 0xFF
		var cb := f.get_8() & 0xFF
		var ca := f.get_8() & 0xFF
		palette.push_back(Color8(cr, cg, cb, ca))

	# read atlases
	for atlas in range(atlas_count):
		var atlas_lump = LUMP_ATLAS0 + atlas
		var atlas_lump_ofs = lump_ofs[atlas_lump]
		var this_atlas_shapes = []
		var shape_count = lump_len[atlas_lump] / 16

		var img: Image = null
		if web_image != null:
			img = web_image
		else:
			img = Image.new()
			img.load(path_base_dir + "/atlas." + str(atlas) + ".png")

		var itex: ImageTexture = ImageTexture.new()
		itex.create_from_image(img, 4)

		f.seek(atlas_lump_ofs)
		for _i in range(shape_count):
			var tlx := float(f.get_16() & 0xFFFF) / 65535.0
			var tly := float(f.get_16() & 0xFFFF) / 65535.0
			var brx := float(f.get_16() & 0xFFFF) / 65535.0
			var bry := float(f.get_16() & 0xFFFF) / 65535.0
			var w := f.get_float()
			var h := f.get_float()

			var tl = Vector2(tlx, 1.0 - tly) * img.get_size()
			var br = Vector2(brx, 1.0 - bry) * img.get_size()

			this_atlas_shapes.push_back([Rect2(tl, br - tl), Vector2(w, h)])

		atlas_shapes.push_back(this_atlas_shapes)
		atlas_textures.push_back(itex)

	# read pages
	for page in range(page_count):
		var page_lump = LUMP_ATLAS0 + atlas_count + page
		var page_lump_ofs: int = lump_ofs[page_lump]
		var page_lump_len: int = lump_len[page_lump]
		f.seek(page_lump_ofs)
		var atlas_id := f.get_8() & 0xFF
		var page_w := f.get_float()
		var page_h := f.get_float()
		var sprites: PoolByteArray = f.get_data(page_lump_len - 9)[1]
		page_atlases.push_back(atlas_id)
		page_sizes.push_back(Vector2(page_w, page_h))
		page_sprites.push_back(sprites)
