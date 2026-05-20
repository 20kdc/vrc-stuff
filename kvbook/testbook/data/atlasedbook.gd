class_name AtlasedBook
extends Reference

const LUMP_METADATA = 0
const LUMP_PALETTE = 1
const LUMP_ATLAS0 = 2

# array of arrays of shape arrays [src: Rect2, size: Vector2]
# source is expressed as 0-1 UVs that have already been flipped
var atlases: Array = []
# array of Color
var palette: PoolColorArray = PoolColorArray()
# array of page descriptors {atlas_id: int, size: Vector2, sprites_base_ofs: int, sprites_count: int}
var pages: Array = []

var buf: StreamPeerBuffer
var version: int = 0
var page_count: int = 0

func _init(bytes: PoolByteArray):
	buf = StreamPeerBuffer.new()
	buf.data_array = bytes
	buf.big_endian = false
	var atlas_count = buf.get_16() & 0xFFFF
	version = buf.get_16() & 0xFFFF
	page_count = buf.get_32() & 0xFFFFFFFF
	# read palette
	var palette_ofs = lump_pos(LUMP_PALETTE)
	var total = lump_size(LUMP_PALETTE) / 4
	buf.seek(palette_ofs)
	while palette.size() < total:
		var cr := buf.get_8() & 0xFF
		var cg := buf.get_8() & 0xFF
		var cb := buf.get_8() & 0xFF
		var ca := buf.get_8() & 0xFF
		palette.push_back(Color8(cr, cg, cb, ca))
	# read atlases
	for atlas in range(atlas_count):
		var atlas_lump = LUMP_ATLAS0 + atlas
		var atlas_lump_ofs = lump_pos(atlas_lump)
		var atlas_shapes = []
		var shape_count = lump_size(atlas_lump) / 16

		buf.seek(atlas_lump_ofs)
		while atlas_shapes.size() < shape_count:
			var tlx = float(buf.get_16() & 0xFFFF) / 65535.0
			var tly = float(buf.get_16() & 0xFFFF) / 65535.0
			var brx = float(buf.get_16() & 0xFFFF) / 65535.0
			var bry = float(buf.get_16() & 0xFFFF) / 65535.0
			var w = buf.get_float()
			var h = buf.get_float()

			var tl = Vector2(tlx, 1.0 - tly)
			var br = Vector2(brx, 1.0 - bry)

			atlas_shapes.push_back([Rect2(tl, br - tl), Vector2(w, h)])
		atlases.push_back(atlas_shapes)
	for page in range(page_count):
		var page_lump = LUMP_ATLAS0 + atlas_count + page
		var page_lump_ofs = lump_pos(page_lump)
		var page_lump_len = lump_size(page_lump)
		buf.seek(page_lump_ofs)
		var atlas_id = buf.get_8() & 0xFF
		var page_w = buf.get_float()
		var page_h = buf.get_float()
		pages.push_back({
			"atlas_id": atlas_id,
			"size": Vector2(page_w, page_h),
			"sprites_ofs": page_lump_ofs + 9,
			"sprites_count": (page_lump_len - 9) / 8
		})

# !!!seeks!!!
func lump_pos(i: int) -> int:
	buf.seek(8 + (i * 8))
	return buf.get_32()

# !!!seeks!!!
func lump_size(i: int) -> int:
	buf.seek(12 + (i * 8))
	return buf.get_32()
