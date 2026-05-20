class_name FileBox
extends Node

signal file_selected(path)

export var path = "" setget set_path
export var verb = "Verb" setget set_verb

func set_verb(v):
	verb = v
	$Button.text = verb

func set_path(p):
	path = p
	$LineEdit.text = p

func _on_LineEdit_text_entered(new_text):
	path = new_text
	emit_signal("file_selected", path)

func _on_Button_pressed():
	emit_signal("file_selected", path)

func _on_Button2_pressed():
	$FileDialog.current_dir = path.get_base_dir()
	$FileDialog.current_file = path.get_file()
	$FileDialog.popup_centered(get_viewport().size / 2)

func _on_FileDialog_file_selected(p2):
	set_path(p2)
	emit_signal("file_selected", p2)
