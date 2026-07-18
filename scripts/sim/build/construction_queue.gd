class_name ConstructionQueue
extends RefCounted
## Sequential build queue: one order in progress at a time. Money is
## deducted on completion, not on enqueue — matches the design doc. An
## order that can no longer be afforded when it completes (balance moved
## under it from other spending) is dropped with no effect on the grid.

var orders: Array[BuildOrder] = []


## Rejects up front if the order alone is unaffordable right now.
func enqueue(order: BuildOrder, ledger: Ledger) -> bool:
	if order.cost > ledger.balance:
		return false
	orders.append(order)
	return true


func tick(grid: SimGrid, ledger: Ledger, events: SimEventBus) -> void:
	if orders.is_empty():
		return
	var order: BuildOrder = orders[0]
	order.ticks_remaining -= 1
	if order.ticks_remaining > 0:
		return
	orders.pop_front()
	if not ledger.spend(order.cost, "construction"):
		events.emit("construction_failed", {"kind": order.kind, "x": order.x, "y": order.y})
		return
	_apply(order, grid)
	events.emit("construction_completed", {"kind": order.kind, "x": order.x, "y": order.y})


func _apply(order: BuildOrder, grid: SimGrid) -> void:
	match order.kind:
		BuildOrder.Kind.WALL:
			grid.set_wall(order.x, order.y, order.wall_flag, true)
		BuildOrder.Kind.DOOR:
			grid.set_door(order.x, order.y, order.wall_flag, true)
		BuildOrder.Kind.FLOOR:
			grid.set_floor(order.x, order.y, order.floor_type)
		BuildOrder.Kind.OBJECT:
			grid.place_object(order.x, order.y, order.object_type)


func to_dict() -> Dictionary:
	var order_data: Array = []
	for o in orders:
		order_data.append(o.to_dict())
	return {"orders": order_data}


func from_dict(d: Dictionary) -> void:
	orders.clear()
	for od in d.get("orders", []):
		orders.append(BuildOrder.from_dict(od))
