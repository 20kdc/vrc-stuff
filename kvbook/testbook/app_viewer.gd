extends PanelContainer

var book: AtlasedBook = null
var current_page: int = 0
var page_count: int = 0

func _ready():
	reload_book()

func _on_Button_pressed():
	reload_book()

func reload_book():
	var le := $"%LineEdit"
	book = AtlasedBookLoader.load(le.text, le.text.ends_with(".png"))
	page_count = 0
	if book != null:
		page_count = book.page_count
	$"%ItemList".clear()
	for i in range(page_count):
		$"%ItemList".add_item(str(i))
	page_update()

func page_update():
	var page: PageRenderView = $"%page"
	if book != null:
		page.page = AtlasedBookPageAccess.new(book, current_page)
	else:
		page.page = null
	page.update()

func _on_ItemList_item_selected(index):
	current_page = index
	page_update()

func _on_LineEdit_text_entered(_new_text):
	reload_book()
