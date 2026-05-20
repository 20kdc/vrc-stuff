class_name AtlasedBookLoaderPile
extends AtlasedBookLoader

var pile: Dictionary

func get_file(name: String) -> String:
	return pile.get(name, "")

func get_atlas(idx: int) -> Image:
	var img := Image.new()
	var data := get_file("atlas." + str(idx) + ".png")
	img.load_png_from_buffer(Marshalls.base64_to_raw(data))
	return img

func load_book() -> AtlasedBook:
	var f := StreamPeerBuffer.new()
	f.big_endian = false
	f.data_array = Marshalls.base64_to_raw(get_file("book.bytes"))
	return load_data(f)
