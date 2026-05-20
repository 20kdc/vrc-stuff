# Drawbook IPC handler
# This singleton must live!
extends Node

var task_queue := []
var has_current_task = false

func get_project_or_executable_path():
	if OS.has_feature("editor"):
		# In editor. Current directory is therefore executable directory
		return "."
	else:
		return OS.get_executable_path().get_base_dir()

func find_me():
	var my_path = get_project_or_executable_path()
	var candidates = []
	if OS.has_feature("windows"):
		# 'feature'
		candidates.push_back(my_path + "/drawbook.bat")
		candidates.push_back(my_path + "/drawbook.exe")
	else:
		candidates.push_back(my_path + "/drawbook")
	for v in candidates:
		var f = File.new()
		if f.file_exists(v):
			return v
	return null

func launch_task(task: IPCTaskBase, who, what: String, binds = []):
	task.connect("completed", who, what, binds)
	task_queue.push_back(task)

func _clear_current_task(_ignore):
	workspace_clear()
	has_current_task = false

func _ready():
	print("workspace manager booting @ " + OS.get_user_data_dir())
	var dir := Directory.new()
	dir.open("user://")
	dir.make_dir_recursive("TempFilesWillBeDeleted")
	workspace_clear()

func _process(_delta):
	if not has_current_task:
		if task_queue.size() > 0:
			var task = task_queue[0]
			task_queue.remove(0)
			has_current_task = true
			task.connect("completed", self, "_clear_current_task")
			add_child(task)

func workspace_clear():
	var dir := Directory.new()
	if dir.open("user://TempFilesWillBeDeleted") != OK:
		print("aborted ws clear: error opening dir")
		return
	if dir.list_dir_begin(true, true) != OK:
		print("aborted ws clear: error listing")
		return
	# remove all existing files
	while true:
		var f = dir.get_next()
		if f == "":
			break
		dir.remove(f)
		print("removed: " + f)

func workspace_file(name: String) -> File:
	var f = File.new()
	if f.open("user://TempFilesWillBeDeleted/" + name, File.WRITE) != OK:
		return null
	return f

func _exit_tree():
	print("workspace manager is closing")
	workspace_clear()
