extends Control
class_name UIBase

@export var auto_destroy := true

var parent_ui: UIBase = null
var children_ui: Array[UIBase] = []

func on_create() -> void:
	pass

func on_refresh() -> void:
	pass

func on_bind() -> void:
	pass

func on_unbind() -> void:
	pass

func on_show() -> void:
	pass

func on_hide() -> void:
	pass

func on_died() -> void:
	pass

func hide_self(force_destroy: bool = false) -> void:
	UIFrame.hide_ui(self, force_destroy)
