class_name AtlasedBookLoaderWeb
extends AtlasedBookLoader

var image: Image

func get_atlas(_idx: int) -> Image:
	return image

func load_book() -> AtlasedBook:
	var f := StreamPeerBuffer.new()
	f.big_endian = false
	f.data_array = image.data["data"]
	return load_data(f)
