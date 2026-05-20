class_name AtlasedBookLoader
extends Reference

const LUMP_METADATA = 0
const LUMP_PALETTE = 1
const LUMP_ATLAS0 = 2

func get_atlas(_idx: int) -> Image:
	return null

func load_data(f: StreamPeerBuffer):
	var res := AtlasedBook.new()
	res.buf = f

	var atlas_count = f.get_16() & 0xFFFF
	res.version = f.get_16() & 0xFFFF
	if res.version < 0x100 or res.version > 0x1FF:
		print("AtlasedBook: Incompatible version or invalid file")
		return null
	res.page_count = f.get_32() & 0xFFFFFFFF
	# lumps
	var lump_ofs := []
	var lump_len := []
	for _i in range(LUMP_ATLAS0 + atlas_count + res.page_count):
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
		res.palette.push_back(Color8(cr, cg, cb, ca))

	# read atlases
	for atlas in range(atlas_count):
		var atlas_lump = LUMP_ATLAS0 + atlas
		var atlas_lump_ofs = lump_ofs[atlas_lump]
		var this_atlas_shapes = []
		var shape_count = lump_len[atlas_lump] / 16

		var img: Image = get_atlas(atlas)

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

		res.atlas_shapes.push_back(this_atlas_shapes)
		res.atlas_textures.push_back(itex)

	# read pages
	for page in range(res.page_count):
		var page_lump = LUMP_ATLAS0 + atlas_count + page
		var page_lump_ofs: int = lump_ofs[page_lump]
		var page_lump_len: int = lump_len[page_lump]
		f.seek(page_lump_ofs)
		var atlas_id := f.get_8() & 0xFF
		var page_w := f.get_float()
		var page_h := f.get_float()
		res.page_atlases.push_back(atlas_id)
		res.page_sizes.push_back(Vector2(page_w, page_h))
		res.page_sprites_ofs.push_back(f.get_position())
		res.page_sprites_count.push_back((page_lump_len - 9) / 8)

	return res
