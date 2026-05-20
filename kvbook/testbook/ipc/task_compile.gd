class_name IPCTaskCompile
extends IPCTaskBase

var args := []
var supplied_file := PoolByteArray()

func _task():
	var output := []
	var args_ps := PoolStringArray(args)
	if len(supplied_file) > 0:
		var f = Drawbook.workspace_file("compile.svg")
		f.store_buffer(supplied_file)
		f.flush()
		args_ps.push_back(f.get_path_absolute())
		f.close()
	OS.execute(Drawbook.find_me(), args_ps, true, output, false)
	return files_parse(output)
