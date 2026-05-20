# Responsible for managing a thread.
class_name IPCTaskBase
extends Node

var description: String = "Observing"
var thread: Thread

signal completed(val)

func _ready():
	if Drawbook.debug_on_main_thread:
		emit_signal("completed", _task())
	else:
		thread = Thread.new()
		thread.start(self, "_ready_intern", null)

func _ready_intern(_userdata):
	return _task()

# Implement task here!
func _task():
	pass

# 'Pile' format stops Godot from crashing due to stdio overwhelming pool vectors
func read_pile(path: String) -> Dictionary:
	var f := File.new()
	var dict := {}
	if f.open(path, File.READ) != OK:
		return dict
	var metadata := PoolByteArray()
	while true:
		var x = f.get_buffer(1)
		if len(x) != 1:
			break
		if x[0] == 0:
			break
		metadata.push_back(x[0])
	var metadata_json = metadata.get_string_from_utf8()
	var parse := JSON.parse(metadata_json)
	if typeof(parse.result) == TYPE_ARRAY:
		for v in parse.result:
			var buf = f.get_buffer(v[1])
			dict[v[0]] = Marshalls.raw_to_base64(buf)
	return dict

func _process(_delta):
	if thread != null and thread.is_active() and not thread.is_alive():
		emit_signal("completed", thread.wait_to_finish())
		queue_free()

func _exit_tree():
	if thread != null and thread.is_active():
		emit_signal("completed", thread.wait_to_finish())
