extends Button

export var app_path: String

func _pressed():
	get_tree().change_scene(app_path)
