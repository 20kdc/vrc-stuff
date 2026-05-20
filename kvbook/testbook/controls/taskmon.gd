extends Label

func _ready():
	_do_describe()
	Drawbook.connect("current_task_updated", self, "_do_describe")

func _do_describe():
	var res = Drawbook.current_task
	if res == null:
		text = "Idle"
	else:
		text = res
