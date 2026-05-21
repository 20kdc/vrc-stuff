class_name IPCTaskExport
extends IPCTaskBase

var args := []
var supplied_file := PoolByteArray()

func _task():
	var args_ps := PoolStringArray(args)
	if len(supplied_file) > 0:
		var f = Drawbook.workspace_file("compile.svg")
		f.store_buffer(supplied_file)
		f.flush()
		args_ps.push_back(f.get_path_absolute())
		f.close()
	var output = []
	OS.execute(Drawbook.find_me(), args_ps, true, output, true)
	if len(output) > 0:
		return output[0]
	return "<unknown error>"
