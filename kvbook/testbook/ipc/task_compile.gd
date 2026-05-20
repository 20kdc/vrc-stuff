class_name IPCTaskCompile
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
	var f2 := Drawbook.workspace_file("compile.pile")
	var f2abs := f2.get_path_absolute()
	args_ps.push_back("--ipc-pile")
	args_ps.push_back(f2abs)
	f2.close()
	OS.execute(Drawbook.find_me(), args_ps, true)
	return read_pile(f2abs)
