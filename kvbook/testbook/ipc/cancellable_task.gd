class_name IPCCancellableTask
extends Node

var held_task: IPCTaskBase
var held_task_who
var held_task_method: String = ""
var held_task_binds: Array = []

# Submits a new task. If a task is previously held by this node, it is cancelled.
func submit(task, who, method, binds = []):
	cancel()
	held_task = task
	held_task_who = who
	held_task_method = method
	held_task_binds = binds

func _process(_delta):
	if held_task != null:
		if Drawbook.try_task(held_task, held_task_who, held_task_method, held_task_binds):
			# ownership transferred
			held_task = null
			_clear_other()

func cancel():
	if held_task != null:
		held_task.queue_free()
		held_task = null
		_clear_other()

func _clear_other():
	held_task_who = null
	held_task_method = ""
	held_task_binds = []

func _exit_tree():
	if held_task != null:
		held_task.queue_free()
		held_task = null
