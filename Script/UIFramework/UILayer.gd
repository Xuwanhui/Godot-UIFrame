extends RefCounted
class_name UILayer

const PANEL := "PanelLayer"
const WINDOW := "WindowLayer"

static func order(name: StringName) -> int:
	if name == &"PanelLayer":
		return 1
	if name == &"WindowLayer":
		return 3
	return 99
