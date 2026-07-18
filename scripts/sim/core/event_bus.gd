class_name SimEventBus
extends RefCounted
## Plain observer list for sim -> view communication.
## Not Godot signals: the sim layer has no Nodes and must stay engine-free.
## Subscribers are Callables taking (event_name: String, payload: Dictionary).

var _subscribers: Array[Callable] = []


func subscribe(cb: Callable) -> void:
	if not _subscribers.has(cb):
		_subscribers.append(cb)


func unsubscribe(cb: Callable) -> void:
	_subscribers.erase(cb)


func emit(event_name: String, payload: Dictionary = {}) -> void:
	for cb in _subscribers:
		cb.call(event_name, payload)


func clear() -> void:
	_subscribers.clear()
