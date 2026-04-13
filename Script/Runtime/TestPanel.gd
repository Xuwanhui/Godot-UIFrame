extends UINode
class_name TestPanel

@export var test_button: Button
var ui_layer: StringName = &"PanelLayer"

func _enter_tree() -> void:
	print("TestPanel entered tree")

func _exit_tree() -> void:
	print("TestPanel exited tree")

func _ready() -> void:
	print("TestPanel ready")

func on_create() -> void:
	print("TestPanel created")
	await get_tree().create_timer(3.0).timeout

func on_refresh() -> void:
	var value := ""
	if data != null and data is TestPanelData:
		value = data.test_string
	print("TestPanel refreshed Data:%s" % value)

func on_bind() -> void:
	test_button.pressed.connect(_on_close_pressed)
	print("TestPanel bind")

func on_unbind() -> void:
	if test_button.pressed.is_connected(_on_close_pressed):
		test_button.pressed.disconnect(_on_close_pressed)
	print("TestPanel unbind")

func on_show() -> void:
	print("TestPanel show")

func on_hide() -> void:
	print("TestPanel hide")

func on_died() -> void:
	print("TestPanel died")

func _on_close_pressed() -> void:
	hide_self()
