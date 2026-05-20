extends Control

export var target: NodePath

func _ready():
	var target_view: PageRenderView = get_node(target)
	$bkg.pressed = target_view.draw_background
	$bkgc.color = target_view.background
	$content.pressed = target_view.draw_content
	$debug.pressed = target_view.draw_debug
	$fco.pressed = target_view.draw_fullcolouronly
	$bkg.connect("toggled", self, "_propagate")
	$bkgc.connect("color_changed", self, "_propagate")
	$content.connect("toggled", self, "_propagate")
	$debug.connect("toggled", self, "_propagate")
	$fco.connect("toggled", self, "_propagate")
	target_view.connect("set_page", self, "_statistics")

func _propagate(_ignore = null):
	var target_view: PageRenderView = get_node(target)
	target_view.draw_background = $bkg.pressed
	target_view.background = $bkgc.color
	target_view.draw_content = $content.pressed
	target_view.draw_debug = $debug.pressed
	target_view.draw_fullcolouronly = $fco.pressed
	target_view.update()

func _statistics(page: AtlasedBookPageAccess):
	if page != null:
		$Label.text = str(page.sprite_count) + " sprites\n" + str(page.page_size.x) + "x" + str(page.page_size.y)
	else:
		$Label.text = "invalid"
