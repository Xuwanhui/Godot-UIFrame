extends RefCounted
class_name UIBaseExtensions

static func breadth_traversal(root: UIBase) -> Array[UIBase]:
	var result: Array[UIBase] = []
	if root == null:
		return result
	var queue: Array[UIBase] = [root]
	while not queue.is_empty():
		var node := queue.pop_front()
		result.append(node)
		for child in node.children_ui:
			queue.append(child)
	return result
