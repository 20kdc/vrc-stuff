extends Camera2D

var dragging = false
var zooming = false
var zoom_val = 1.0

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
				zoom_val = max(0.001, zoom_val + (mm.relative.y / 256.0))
				zoom = Vector2(zoom_val, zoom_val)
			else:
				position += mm.relative * zoom_val
			get_tree().set_input_as_handled()
