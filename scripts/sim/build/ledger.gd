class_name Ledger
extends RefCounted

var balance: int
var transactions: Array[Dictionary] = []


func _init(starting_balance: int = 0) -> void:
	balance = starting_balance


func deposit(amount: int, reason: String) -> void:
	assert(amount >= 0)
	balance += amount
	transactions.append({"amount": amount, "reason": reason, "balance_after": balance})


## Fails (returns false, no state change) if it would overdraw the balance.
func spend(amount: int, reason: String) -> bool:
	assert(amount >= 0)
	if amount > balance:
		return false
	balance -= amount
	transactions.append({"amount": -amount, "reason": reason, "balance_after": balance})
	return true


func to_dict() -> Dictionary:
	return {"balance": balance, "transactions": transactions.duplicate(true)}


func from_dict(d: Dictionary) -> void:
	balance = int(d.get("balance", 0))
	transactions.clear()
	for t in d.get("transactions", []):
		transactions.append((t as Dictionary).duplicate(true))
