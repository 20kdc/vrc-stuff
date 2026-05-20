class_name IPCTaskTest
extends IPCTaskBase

func _task():
	var output := []
	var res = OS.execute(Drawbook.find_me(), PoolStringArray(["--ipc-test"]), true, output, true)
	if output.size() > 0:
		return [res, output[0]]
	return [res, null]
