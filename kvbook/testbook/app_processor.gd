extends Node

var pages: Array = []
var inputlib_last = 0
var current_page = 0

func _ready():
	if OS.has_feature("editor"):
		$"%input_filebox".path = "../drawbook/book_tp.pdf"

func _on_filebox_file_selected(_path):
	_queue_inputlib()

func _on_mutool_draw_text_entered(_new_text):
	_queue_inputlib()

func _queue_inputlib():
	var task := IPCTaskCompile.new()
	for v in $"%mutool_draw".text.split(" "):
		if v == "":
			continue
		task.args.push_back("-m")
		task.args.push_back(v)
	task.args.push_back("--ipc-inputlib")
	var path: String = $"%input_filebox".path
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

func discard_and_queue_preview(_index):
	_queue_preview()

func _on_ItemList_item_selected(index):
	current_page = index
	_queue_preview()

# Adds render args.
func add_render_args(args: Array):
	# colour_type
	var colour_type = $"%colour_type".selected
	if colour_type == 1:
		# SDF everything
		args.push_back("--sdf-everything")
	elif colour_type == 2:
		# BW dither
		args.push_back("--no-fullcolour")
	elif colour_type == 3:
		# SDF off
		args.push_back("--fullcolour-blue")
		args.push_back("0")
	# render multipliers and limits
	args.push_back("--render-mul")
	args.push_back(str($"%vec_render_mul".value))
	args.push_back("--render-limit")
	args.push_back(str($"%vec_render_limit".value))
	args.push_back("--img-render-mul")
	args.push_back(str($"%img_render_mul".value))
	args.push_back("--img-render-limit")
	args.push_back(str($"%img_render_limit".value))

func _queue_preview():
	if current_page < 0 or current_page >= len(pages):
		return
	var task := IPCTaskCompile.new()
	add_render_args(task.args)
	task.supplied_file = Marshalls.base64_to_raw(pages[current_page])
	task.description = "Previewing " + str(current_page)
	$task_preview.submit(task, self, "_finish_preview")

func _finish_preview(files: Dictionary):
	print(files.keys())
	var pile = AtlasedBookLoaderPile.new()
	pile.pile = files
	$"%PageRenderView".page = AtlasedBookPageAccess.new(pile.load_book(), 0)
	$"%PageRenderView".update()

func _on_xopt_filebox_verb_pressed(path):
	# do export
	var task := IPCTaskExport.new()
	add_render_args(task.args)

	if not $"%xopt_dae".pressed:
		task.args.push_back("--no-dae")

	if $"%xopt_web".pressed:
		task.args.push_back("--web")
	else:
		task.args.push_back("--atlas-min-size")
		task.args.push_back(str($"%xopt_atlas_min".value))
		task.args.push_back("--atlas-max-size")
		task.args.push_back(str($"%xopt_atlas_max".value))

	var builder: PoolByteArray = PoolByteArray()
	for v in pages:
		builder.append_array(Marshalls.base64_to_raw(v))
		# separate SVGs
		builder.push_back(0)
	task.supplied_file = builder

	task.args.push_back("-o")
	task.args.push_back(path)

	task.args.push_back("-q")

	task.description = "Exporting..."
	$"%console".text = "Please wait..."
	$task_export.submit(task, self, "_finish_export")

func _finish_export(result):
	$"%console".text = result
