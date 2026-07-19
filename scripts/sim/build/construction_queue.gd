class_name ConstructionQueue
extends RefCounted
## A pool of pending build orders that workers claim and burn down. Money is
## deducted on completion, not on enqueue — matches the design doc. An order
## that can no longer be afforded when it completes (balance moved under it
## from other spending) is dropped with no effect on the grid.
##
## Before M3 this was a strict FIFO countdown that advanced on its own. Now
## the queue never advances itself: StaffAI claims orders for workers and
## calls apply_work(). Orders are still handed out in enqueue order, so the
## player's intent is respected, but N workers progress N orders at once.

## Sentinel claim marking an order no worker can currently reach. Distinct
## from an unclaimed order so workers stop re-claiming it every minute;
## reset_blocked_claims() frees them again once the map changes.
const BLOCKED_CLAIM := -2

var orders: Array[BuildOrder] = []
var next_order_id: int = 0


## Rejects up front if the order alone is unaffordable right now.
func enqueue(order: BuildOrder, ledger: Ledger) -> bool:
	if order.cost > ledger.balance:
		return false
	order.id = next_order_id
	next_order_id += 1
	orders.append(order)
	return true


func order_by_id(order_id: int) -> BuildOrder:
	for o in orders:
		if o.id == order_id:
			return o
	return null


## Oldest order nobody has claimed, or null. Deliberately not "nearest to the
## worker" — the player queues in the order they want things built, and a
## nearest-first pool would reorder their intent behind their back.
func claim_next(worker_id: int) -> BuildOrder:
	for o in orders:
		if o.claimed_by == -1:
			o.claimed_by = worker_id
			return o
	return null


## Drop a worker's claim without losing the progress already burned in (a
## staffer going off shift, quitting, or getting reassigned).
func release_claims_of(worker_id: int) -> void:
	for o in orders:
		if o.claimed_by == worker_id:
			o.claimed_by = -1


## Make unreachable orders claimable again. Called when the grid changes,
## since a new door or a demolished wall may have opened a route in.
func reset_blocked_claims() -> void:
	for o in orders:
		if o.claimed_by == BLOCKED_CLAIM:
			o.claimed_by = -1


## Burn worker-ticks into an order; completes it when the work runs out.
## Returns true on the tick the order completes (successfully or not).
func apply_work(order: BuildOrder, amount: float, grid: SimGrid, ledger: Ledger, events: SimEventBus) -> bool:
	order.work_remaining -= amount
	if order.work_remaining > 0.0:
		return false
	orders.erase(order)
	if not ledger.spend(order.cost, "construction"):
		events.emit("construction_failed", {"kind": order.kind, "x": order.x, "y": order.y})
		return true
	_apply(order, grid)
	events.emit("construction_completed", {"kind": order.kind, "x": order.x, "y": order.y})
	return true


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
	return {"orders": order_data, "next_order_id": next_order_id}


func from_dict(d: Dictionary) -> void:
	orders.clear()
	for od in d.get("orders", []):
		orders.append(BuildOrder.from_dict(od))
	next_order_id = int(d.get("next_order_id", 0))
