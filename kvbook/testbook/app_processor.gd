extends Node

var pages: Array = []
var inputlib_last = 0
var current_page = 0

func _ready():
	if OS.has_feature("editor"):
		$"%filebox".path = "../drawbook/book_tp.pdf"

func _on_filebox_file_selected(_path):
	_queue_inputlib()

func _queue_inputlib():
	var task := IPCTaskCompile.new()
	task.args.push_back("--ipc-inputlib")
	var path: String = $"%filebox".path
	task.args.push_back(path)
	task.description = "Splitting " + path
	inputlib_last = Drawbook.alloc_task_id()
	$task_inputlib.submit(task, self, "_finish_inputlib", [inputlib_last])
	$task_preview.cancel()

func _finish_inputlib(files: Dictionary, last: int):
	# print(pages_a, last)
	if inputlib_last != last:
		return
	# parse pages
	pages = []
	var page_idx := 0
	while true:
		var fn := str(page_idx) + ".svg"
		var data = files.get(fn)
		if data == null:
			break
		pages.push_back(data)
		page_idx += 1
	# continue
	$"%ItemList".clear()
	for i in range(len(pages)):
		$"%ItemList".add_item(str(i))
	if len(pages) > 0:
		if current_page >= len(pages):
			current_page = len(pages) - 1
		$"%ItemList".select(current_page)
		_queue_preview()

func _on_ItemList_item_selected(index):
	current_page = index
	_queue_preview()

func _queue_preview():
	if current_page < 0 or current_page >= len(pages):
		return
	var task := IPCTaskCompile.new()
	task.supplied_file = Marshalls.base64_to_raw(pages[current_page])
	task.description = "Previewing " + str(current_page)
	$task_preview.submit(task, self, "_finish_preview")

func _finish_preview(files: Dictionary):
	print(files.keys())
	var pile = AtlasedBookLoaderPile.new()
	pile.pile = files
	$"%PageRenderView".page = AtlasedBookPageAccess.new(pile.load_book(), 0)
	$"%PageRenderView".update()
