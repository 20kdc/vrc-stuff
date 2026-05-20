# Drawbook IPC handler
# This singleton must live!
extends Node

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
