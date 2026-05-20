# Drawbook IPC handler
# This singleton must live!
extends Node

var current_task = null

var last_task_id: int = 0

signal current_task_updated()

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

func alloc_task_id() -> int:
	last_task_id += 1
	return last_task_id

# Tries to launch a task.
# This may fail if a task is already running.
# Use an IPCCancellableTask wrapper for eventual execution.
func try_task(task: IPCTaskBase, who, what: String, binds = []) -> bool:
	if current_task != null:
		return false
	current_task = "Running: " + task.description
	task.connect("completed", who, what, binds)
	task.connect("completed", self, "_clear_current_task")
	add_child(task)
	emit_signal("current_task_updated")
	return true

func _clear_current_task(_ignore):
	workspace_clear()
	current_task = null
	emit_signal("current_task_updated")

func _ready():
	print("workspace manager booting @ " + OS.get_user_data_dir())
	var dir := Directory.new()
	dir.open("user://")
	dir.make_dir_recursive("TempFilesWillBeDeleted")
	workspace_clear()

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
