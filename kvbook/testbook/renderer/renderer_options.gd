extends Control

export var target: NodePath

func _ready():
	var target_view: PageRenderView = get_node(target)
	$v/bkg.pressed = target_view.draw_background
	$v/bkgc.color = target_view.background
	$v/content.pressed = target_view.draw_content
	$v/debug.pressed = target_view.draw_debug
	$v/fco.pressed = target_view.draw_fullcolouronly
	$v/bkg.connect("toggled", self, "_propagate")
	$v/bkgc.connect("color_changed", self, "_propagate")
	$v/content.connect("toggled", self, "_propagate")
	$v/debug.connect("toggled", self, "_propagate")
	$v/fco.connect("toggled", self, "_propagate")
	target_view.connect("set_page", self, "_statistics")

func _propagate(_ignore = null):
	var target_view: PageRenderView = get_node(target)
	target_view.draw_background = $v/bkg.pressed
	target_view.background = $v/bkgc.color
	target_view.draw_content = $v/content.pressed
	target_view.draw_debug = $v/debug.pressed
	target_view.draw_fullcolouronly = $v/fco.pressed
	target_view.update()

func _statistics(page: AtlasedBookPageAccess):
	if page != null:
		$v/Label.text = str(page.sprite_count) + " sprites\n" + str(page.page_size.x) + "x" + str(page.page_size.y)
	else:
		$v/Label.text = "invalid"
