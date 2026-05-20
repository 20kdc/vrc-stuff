# PageRenderView renders a page.
class_name PageRenderView
extends Control

var page: AtlasedBookPageAccess setget set_page
var background: Color = Color.white
var draw_background: bool = true
var draw_content: bool = true
var draw_debug: bool = false
var draw_fullcolouronly: bool = false setget _set_drawfullcolouronly

var camera_pos: Vector2
var camera_zoom: float = 1.0

signal set_page(page)

func _set_drawfullcolouronly(val):
	draw_fullcolouronly = val
	if val:
		material = null
	else:
		material = preload("pagerendermaterial.tres")

func set_page(ipage):
	page = ipage
	emit_signal("set_page", page)

func _gui_input(event):
	if event is InputEventMouseMotion:
		var mouse := event as InputEventMouseMotion
		if page != null and (mouse.button_mask & 1) != 0:
			camera_pos -= mouse.relative / camera_zoom
			camera_pos = Vector2(
				max(0, min(camera_pos.x, page.page_size.x)),
				max(0, min(camera_pos.y, page.page_size.y))
			)
			update()
			accept_event()
	elif event is InputEventMouseButton:
		var mouse := event as InputEventMouseButton
		if mouse.pressed:
			if mouse.button_index == BUTTON_WHEEL_DOWN:
				camera_zoom /= 1.1
				camera_zoom = max(camera_zoom, 0.1)
				update()
				accept_event()
			elif mouse.button_index == BUTTON_WHEEL_UP:
				camera_zoom *= 1.1
				camera_zoom = max(camera_zoom, 0.1)
				update()
				accept_event()
			elif mouse.button_index == BUTTON_MIDDLE:
				camera_zoom = 1.0
				update()
				accept_event()

func _draw():
	_drawpass(0)
	if draw_debug:
		_drawpass(1)

# Transforms rectangle from page space to control coordinates.
func transform_rect(rect: Rect2) -> Rect2:
	# figure out camera stuff
	rect.position -= camera_pos
	# need to save this because changing the position will distort it
	var rend := rect.end
	rect.position *= camera_zoom
	rect.end = rend * camera_zoom
	# and now centre
	rect.position += rect_size / 2
	return rect

func _drawpass(drawpass: int):

	if page == null or page.book == null:
		return

	# print("had page and book")

	if drawpass == 0 and draw_background:
		draw_rect(transform_rect(Rect2(Vector2.ZERO, page.page_size)), background)

	var atlas_shapes: Array = page.book.atlas_shapes[page.atlas_id]
	var atlas: Texture = page.book.atlas_textures[page.atlas_id]
	for i in range(page.sprite_count):
		page.read_sprite(i)
		# get shape
		var shape_info: Array = atlas_shapes[page.sprite_shape_id]
		var src: Rect2 = shape_info[0]
		var size: Vector2 = shape_info[1]
		src = Rect2(src.position, src.size)
		# target region
		var targ := transform_rect(Rect2(page.sprite_pos, size))
		# actual draw code
		if drawpass == 0:
			if draw_debug:
				draw_rect(targ, Color.green, false, 2)
			if draw_content:
				draw_texture_rect_region(atlas, targ, src, page.sprite_colour)
		if drawpass == 1:
			draw_rect(targ, Color.red, false)
