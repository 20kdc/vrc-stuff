extends CheckBox

func _toggled(button_pressed):
	Drawbook.debug_on_main_thread = button_pressed
