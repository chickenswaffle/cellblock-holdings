extends GutTest


func test_starting_balance() -> void:
	var l := Ledger.new(1000)
	assert_eq(l.balance, 1000)
	assert_eq(l.transactions.size(), 0)


func test_deposit() -> void:
	var l := Ledger.new(100)
	l.deposit(50, "contract payment")
	assert_eq(l.balance, 150)
	assert_eq(l.transactions.size(), 1)
	assert_eq(l.transactions[0]["amount"], 50)


func test_spend_success() -> void:
	var l := Ledger.new(100)
	var ok := l.spend(40, "wall")
	assert_true(ok)
	assert_eq(l.balance, 60)
	assert_eq(l.transactions[0]["amount"], -40)


func test_spend_insufficient_funds_fails_cleanly() -> void:
	var l := Ledger.new(10)
	var ok := l.spend(40, "wall")
	assert_false(ok)
	assert_eq(l.balance, 10, "balance must not change on a failed spend")
	assert_eq(l.transactions.size(), 0, "no transaction recorded for a failed spend")


func test_serialization_roundtrip() -> void:
	var a := Ledger.new(500)
	a.spend(100, "wall")
	a.deposit(20, "refund")
	var b := Ledger.new(0)
	b.from_dict(a.to_dict())
	assert_eq(b.balance, a.balance)
	assert_eq(b.transactions.size(), 2)
	assert_eq(b.transactions[1]["amount"], 20)
