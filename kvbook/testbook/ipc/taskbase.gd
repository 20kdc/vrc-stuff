# Responsible for managing a thread.
class_name IPCTaskBase
extends Node

const DEBUG_ON_MAIN_THREAD = false

var description: String = "Observing"
var thread: Thread

signal completed(val)

func _ready():
	if DEBUG_ON_MAIN_THREAD:
		emit_signal("completed", _task())
	else:
		thread = Thread.new()
		thread.start(self, "_ready_intern", null)

func _ready_intern(_userdata):
	return _task()

# Implement task here!
func _task():
	pass

func files_parse(output: Array) -> Dictionary:
	var res2 := ""
	if output.size() > 0:
		res2 = output[0]
	var fn = null
	var total := {}
	for line in res2.split("\n"):
		var linex: String = line
		linex = linex.strip_edges()
		if linex == "STATISTICS":
			break
		elif fn == null:
			if linex != "":
				fn = linex
		else:
			total[fn] = Marshalls.base64_to_raw(linex)
			fn = null
	return total

func _process(_delta):
	if thread != null and thread.is_active() and not thread.is_alive():
		emit_signal("completed", thread.wait_to_finish())
		queue_free()

func _exit_tree():
	if thread != null and thread.is_active():
		emit_signal("completed", thread.wait_to_finish())
