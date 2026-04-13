extends Control
class_name StuckPanel

@export var icon: TextureRect

func _process(delta: float) -> void:
	icon.rotation += delta * 5.0
