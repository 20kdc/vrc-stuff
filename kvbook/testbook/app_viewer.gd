extends PanelContainer

var book: AtlasedBook = null
var current_page: int = 0
var page_count: int = 0

func _ready():
	reload_book()

func _on_Button_pressed():
	reload_book()

func load_book_core(path: String):
	if not path.ends_with(".png"):
		var abl := AtlasedBookLoaderFile.new()
		abl.file = File.new()
		if abl.file.open(path, File.READ) != OK:
			return null
		return abl.load_book()
	else:
		var abl := AtlasedBookLoaderWeb.new()
		abl.image = Image.new()
		if abl.image.load(path) != OK:
			return null
		return abl.load_book()

func reload_book():
	var le := $"%filebox"
	book = load_book_core(le.path)
	page_count = 0
	if book != null:
		page_count = book.page_count
	$"%ItemList".clear()
	for i in range(page_count):
		$"%ItemList".add_item(str(i))
	page_update()

func page_update():
	if current_page >= 0 and current_page < page_count:
		$"%ItemList".select(current_page)
	var page: PageRenderView = $"%page"
	if book != null:
		page.page = AtlasedBookPageAccess.new(book, current_page)
	else:
		page.page = null
	page.update()

func _on_ItemList_item_selected(index):
	current_page = index
	page_update()

func _on_filebox_file_selected(_path):
	reload_book()
