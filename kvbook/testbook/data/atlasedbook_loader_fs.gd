class_name AtlasedBookLoaderFile
extends AtlasedBookLoader

var file: File

func get_atlas(idx: int) -> Image:
	var path_base_dir := file.get_path_absolute().get_base_dir()
	var img := Image.new()
	img.load(path_base_dir + "/atlas." + str(idx) + ".png")
	return img

func load_book() -> AtlasedBook:
	var f := StreamPeerBuffer.new()
	f.big_endian = false
	file.seek(0)
	f.data_array = file.get_buffer(file.get_len())
	return load_data(f)
