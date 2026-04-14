extends Camera2D

var dragging = false
var zooming = false

func _input(event):
	if event is InputEventMouseButton:
		if event.button_index == BUTTON_LEFT or event.button_index == BUTTON_RIGHT:
			dragging = event.pressed
			if event.pressed:
				zooming = event.button_index == BUTTON_RIGHT
			get_tree().set_input_as_handled()
	elif event is InputEventMouseMotion:
		if dragging:
			var mm := event as InputEventMouseMotion
			if zooming:
				zoom = zoom + (Vector2(mm.relative.y, mm.relative.y) / 256.0)
			else:
				position += mm.relative
			get_tree().set_input_as_handled()
