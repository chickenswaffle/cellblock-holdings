class_name SimRng
extends RefCounted
## Seeded xorshift64* RNG. The only source of randomness allowed in sim/.
## Deterministic across platforms; state is a single int, trivially savable.

var state: int


func _init(seed_value: int = 1) -> void:
	# State must never be zero for xorshift.
	state = seed_value if seed_value != 0 else 0x2545F4914F6CDD1D


## Next raw 64-bit value (xorshift64*).
func next() -> int:
	var x := state
	x ^= x << 13
	x ^= x >> 7
	x ^= x << 17
	state = x
	return x


## Uniform int in [0, n). n must be > 0.
func randi_range_n(n: int) -> int:
	assert(n > 0)
	var v := next()
	if v < 0:
		v = -(v + 1)
	return v % n


## Uniform int in [lo, hi] inclusive.
func randi_between(lo: int, hi: int) -> int:
	assert(hi >= lo)
	return lo + randi_range_n(hi - lo + 1)


## Uniform float in [0, 1).
func randf01() -> float:
	var v := next()
	if v < 0:
		v = -(v + 1)
	return float(v % 1_000_000_007) / 1_000_000_007.0


## True with probability p.
func chance(p: float) -> bool:
	return randf01() < p


func to_dict() -> Dictionary:
	return {"state": state}


func from_dict(d: Dictionary) -> void:
	state = int(d.get("state", 1))
