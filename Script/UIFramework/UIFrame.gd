extends Node
class_name UIFrame

const STUCK_TIME := 0.5

signal stuck_start
signal stuck_end
signal node_release(ui_name: StringName)
signal ui_create(ui: UIBase)
signal ui_refresh(ui: UIBase)
signal ui_bind(ui: UIBase)
signal ui_unbind(ui: UIBase)
signal ui_show(ui: UIBase)
signal ui_hide(ui: UIBase)
signal ui_died(ui: UIBase)

var _controls: Dictionary = {}
var _panel_stack: Array[Dictionary] = []
var _ui_layers: Dictionary = {}
var _ui_layer_root: CanvasLayer

func _enter_tree() -> void:
	_init_layers()
	await show_by_name(&"TestPanel", preload("res://Script/Runtime/TestPanelData.gd").new("初始化"))

func _init_layers() -> void:
	var canvas_layer := CanvasLayer.new()
	canvas_layer.name = "UILayer"
	var stuck_layer := CanvasLayer.new()
	stuck_layer.name = "StuckLayer"
	add_child(canvas_layer)
	add_child(stuck_layer)
	_ui_layer_root = canvas_layer
	var stuck_panel := load("res://Script/UIFramework/StuckUI/StuckPanel.tscn").instantiate() as Control
	stuck_layer.add_child(stuck_panel)
	stuck_panel.visible = false
	stuck_start.connect(func():
		stuck_panel.visible = true
		stuck_panel.set_process(true)
	)
	stuck_end.connect(func():
		stuck_panel.visible = false
		stuck_panel.set_process(false)
	)

func get_current_panel() -> UIBase:
	if _panel_stack.is_empty():
		return null
	var key: StringName = _panel_stack.back().type
	return _controls.get(key)

func show_by_name(type_name: StringName, data: UIData = null) -> UIBase:
	var layer := _layer_name(type_name)
	if layer == &"":
		push_error("请先在脚本中声明 ui_layer")
		return null
	return await _show_layer_ui(type_name, data)

func hide(force_destroy: bool = false) -> void:
	await _hide_panel(force_destroy)

func hide_ui(ui: UIBase, force_destroy: bool = false) -> void:
	var type_name := StringName(ui.name)
	if _layer_name(type_name) == &"":
		if not ui.visible:
			return
		var ui_bases := UIBaseExtensions.breadth_traversal(ui)
		_do_unbind(ui_bases)
		_do_hide(ui_bases)
		ui.visible = false
		return
	await hide_type(type_name, force_destroy)

func hide_type(type_name: StringName, force_destroy: bool = false) -> void:
	if _layer_name(type_name) == &"PanelLayer":
		await _hide_panel(force_destroy)
		return
	var control: UIBase = _controls.get(type_name)
	if control == null:
		return
	var ui_bases := UIBaseExtensions.breadth_traversal(control)
	_do_unbind(ui_bases)
	_do_hide(ui_bases)
	control.visible = false
	if control.auto_destroy or force_destroy:
		_release_control(type_name)

func refresh_type(type_name: StringName, data: UIData = null) -> void:
	var control: UIBase = _controls.get(type_name)
	if control != null:
		await refresh_ui(control, data)

func refresh_ui(ui: UIBase, data: UIData = null) -> void:
	if not ui.visible:
		return
	var ui_bases := UIBaseExtensions.breadth_traversal(ui)
	_set_data(ui, data)
	await _do_refresh(ui_bases)

func _show_layer_ui(type_name: StringName, data: UIData = null) -> UIBase:
	var current_panel := get_current_panel()
	if _layer_name(type_name) == &"PanelLayer":
		if current_panel != null and StringName(current_panel.name) == type_name:
			return current_panel
		var current_ui_bases: Array[UIBase] = []
		if current_panel != null:
			current_ui_bases = UIBaseExtensions.breadth_traversal(current_panel)
			_do_unbind(current_ui_bases)
		var control := await _load_control(type_name, data)
		if control == null:
			return null
		var ui_bases := UIBaseExtensions.breadth_traversal(control)
		await _do_refresh(ui_bases)
		if current_panel != null:
			_do_hide(current_ui_bases)
			current_panel.visible = false
			if current_panel.auto_destroy:
				_release_control(StringName(current_panel.name))
		control.visible = true
		_panel_stack.append({"type": type_name, "data": data})
		_do_bind(ui_bases)
		_do_show(ui_bases)
		return control
	var window := await _load_control(type_name, data)
	if window == null:
		return null
	var window_bases := UIBaseExtensions.breadth_traversal(window)
	await _do_refresh(window_bases)
	window.visible = true
	window.get_parent().move_child(window, window.get_parent().get_child_count() - 1)
	_do_bind(window_bases)
	_do_show(window_bases)
	return window

func _hide_panel(force_destroy: bool) -> void:
	var current_panel := get_current_panel()
	if current_panel == null:
		return
	var current_ui_bases := UIBaseExtensions.breadth_traversal(current_panel)
	_panel_stack.pop_back()
	_do_unbind(current_ui_bases)
	if not _panel_stack.is_empty():
		var previous := _panel_stack.back()
		var control := await _load_control(previous.type, previous.data)
		var ui_bases := UIBaseExtensions.breadth_traversal(control)
		await _do_refresh(ui_bases)
		current_panel.visible = false
		_do_hide(current_ui_bases)
		if current_panel.auto_destroy or force_destroy:
			_release_control(StringName(current_panel.name))
		control.visible = true
		control.get_parent().move_child(control, control.get_parent().get_child_count() - 1)
		_do_bind(ui_bases)
		_do_show(ui_bases)
	else:
		current_panel.visible = false
		_do_hide(current_ui_bases)
		if current_panel.auto_destroy or force_destroy:
			_release_control(StringName(current_panel.name))

func _load_control(type_name: StringName, data: UIData) -> UIBase:
	if _controls.has(type_name):
		var exist := _controls[type_name] as UIBase
		_set_data(exist, data)
		return exist
	var scene_path := "res://Scene/UI/%s/%s.tscn" % [_layer_name(type_name), String(type_name)]
	var packed := load(scene_path) as PackedScene
	if packed == null:
		push_error("加载失败: %s" % scene_path)
		return null
	var control := packed.instantiate() as UIBase
	if control == null:
		push_error("Control节点没有挂UIBase脚本")
		return null
	var layer := _get_or_create_canvas_layer(type_name)
	await _instantiate(control, layer, data)
	_controls[type_name] = control
	return control

func _instantiate(scene: UIBase, layer: CanvasLayer, data: UIData) -> void:
	scene.visible = false
	layer.add_child(scene)
	var ui_bases := _collect_ui_tree(scene)
	_set_data(scene, data)
	for item in ui_bases:
		item.children_ui.clear()
	for item in ui_bases:
		var parent_ui := _get_parent_ui(item)
		if parent_ui != null:
			parent_ui.children_ui.append(item)
			item.parent_ui = parent_ui
	for item in ui_bases:
		ui_create.emit(item)
		await _await_if_state(item.on_create())
	if _layer_name(StringName(scene.name)) == &"":
		await _do_refresh(ui_bases)
		scene.visible = true
		_do_bind(ui_bases)
		_do_show(ui_bases)

func _collect_ui_tree(root: UIBase) -> Array[UIBase]:
	var result: Array[UIBase] = []
	var stack: Array[Node] = [root]
	while not stack.is_empty():
		var node := stack.pop_front()
		if node is UIBase:
			result.append(node)
		for c in node.get_children():
			stack.append(c)
	return result

func _get_parent_ui(ui: UIBase) -> UIBase:
	var parent := ui.get_parent()
	while parent != null:
		if parent is UIBase:
			return parent
		parent = parent.get_parent()
	return null

func _set_data(ui: UIBase, data: UIData) -> void:
	if data == null:
		return
	for prop in ui.get_property_list():
		if prop.get("name", "") == "data":
			ui.set("data", data)
			break

func _release_control(type_name: StringName) -> void:
	if not _controls.has(type_name):
		return
	var control := _controls[type_name] as UIBase
	_queue_free_ui(control)
	_controls.erase(type_name)
	node_release.emit(type_name)

func _queue_free_ui(control: UIBase) -> void:
	var ui_bases := UIBaseExtensions.breadth_traversal(control)
	for item in ui_bases:
		ui_died.emit(item)
		item.on_died()
	control.queue_free()

func _layer_name(type_name: StringName) -> StringName:
	if type_name == &"TestPanel":
		return &"PanelLayer"
	return &""

func _get_or_create_canvas_layer(type_name: StringName) -> CanvasLayer:
	var name := _layer_name(type_name)
	if name == &"":
		return _ui_layer_root
	if not _ui_layers.has(name):
		var canvas := CanvasLayer.new()
		canvas.name = String(name)
		_ui_layer_root.add_child(canvas)
		_ui_layers[name] = canvas
		var keys := _ui_layers.keys()
		keys.sort_custom(func(a, b): return UILayer.order(a) < UILayer.order(b))
		var index := 1
		for key in keys:
			_ui_layer_root.move_child(_ui_layers[key], index)
			index += 1
	return _ui_layers[name]

func _do_refresh(ui_bases: Array[UIBase]) -> void:
	for i in ui_bases.size():
		var ui := ui_bases[i]
		if ui == null:
			continue
		if i == 0 or ui.visible:
			ui_refresh.emit(ui)
			await _await_if_state(ui.on_refresh())

func _do_bind(ui_bases: Array[UIBase]) -> void:
	for i in ui_bases.size():
		var ui := ui_bases[i]
		if ui == null:
			continue
		if i == 0 or ui.visible:
			ui_bind.emit(ui)
			ui.on_bind()

func _do_unbind(ui_bases: Array[UIBase]) -> void:
	for i in range(ui_bases.size() - 1, -1, -1):
		var ui := ui_bases[i]
		if ui == null:
			continue
		if i == 0 or ui.visible:
			ui_unbind.emit(ui)
			ui.on_unbind()

func _do_show(ui_bases: Array[UIBase]) -> void:
	for i in ui_bases.size():
		var ui := ui_bases[i]
		if ui == null:
			continue
		if i == 0 or ui.visible:
			ui_show.emit(ui)
			ui.on_show()

func _do_hide(ui_bases: Array[UIBase]) -> void:
	for i in range(ui_bases.size() - 1, -1, -1):
		var ui := ui_bases[i]
		if ui == null:
			continue
		if i == 0 or ui.visible:
			ui_hide.emit(ui)
			ui.on_hide()

func _await_if_state(value):
	if value is GDScriptFunctionState:
		await value
	return
