extends Camera2D

var mouse_start_pos
var screen_start_position

var dragging = false

# Lower cap for the `_zoom_level`.
@export var min_zoom := 0.5
# Upper cap for the `_zoom_level`.
@export var max_zoom := 2.0
# Controls how much we increase or decrease the `_zoom_level` on every turn of the scroll wheel.
@export var zoom_factor := 0.1
# Duration of the zoom's tween animation.
@export var zoom_duration := 0.2

# The camera's target zoom level.
var _zoom_level := 1.0


func _input(event):
	if event.is_action("camera_drag"):
		if event.is_pressed():
			mouse_start_pos = event.position
			screen_start_position = position
			dragging = true
		else:
			dragging = false
	elif event is InputEventMouseMotion and dragging:
		position = screen_start_position - zoom * (event.position - mouse_start_pos)

	if event.is_action_pressed("camera_zoom_out"):
		# Inside a given class, we need to either write `self._zoom_level = ...` or explicitly
		# call the setter function to use it.
		_set_zoom_level(_zoom_level - zoom_factor)
	if event.is_action_pressed("camera_zoom_in"):
		_set_zoom_level(_zoom_level + zoom_factor)

func _set_zoom_level(value: float) -> void:
	# We limit the value between `min_zoom` and `max_zoom`
	_zoom_level = clamp(value, min_zoom, max_zoom)
	var tween = get_tree().create_tween()
	tween.tween_property(
		self, "zoom",
		Vector2(_zoom_level, _zoom_level),
		zoom_duration
		).set_trans(Tween.TRANS_SINE).set_ease(Tween.EASE_OUT)
