# Responsible for managing a thread.
class_name IPCTaskBase
extends Node

const DEBUG_ON_MAIN_THREAD = false

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

func _process(_delta):
	if thread != null and thread.is_active() and not thread.is_alive():
		emit_signal("completed", thread.wait_to_finish())
		queue_free()

func _exit_tree():
	if thread != null and thread.is_active():
		emit_signal("completed", thread.wait_to_finish())
