class_name Hiring
extends RefCounted
## Recruiting staff. Generation is RNG-driven (world.rng) so the same seed
## always produces the same hires, same as Intake does for prisoners.

const FIRST_NAMES := [
	"Dana", "Ruth", "Owen", "Priya", "Hector", "Maya", "Curtis", "Ingrid",
	"Rashad", "Lena", "Bogdan", "Simone", "Teddy", "Nadia", "Emeka", "Gail",
]
const LAST_NAMES := [
	"Halloran", "Okafor", "Bianchi", "Stroud", "Nakamura", "Delacroix", "Pike",
	"Abernathy", "Fuentes", "Kowal", "Ostrom", "Bright", "Vasquez", "Lindqvist",
]

## Nerve is rolled once at hire and never changes — fatigue is what moves
## day to day (Staff.effective_nerve()).
const NERVE_MIN := 0.45
const NERVE_MAX := 0.95


## Hire one staffer, charging the up-front fee. Returns null (and hires
## nobody) if the fee is unaffordable. Shift alternates DAY/NIGHT per role so
## a player who just spams the hire key still ends up with night cover.
static func hire(world: SimWorld, role: int) -> Staff:
	var fee := Staff.hiring_fee(role)
	if not world.ledger.spend(fee, "hiring fee"):
		world.events.emit("hire_failed", {"role": role, "fee": fee})
		return null

	var s := Staff.new()
	s.id = world.next_staff_id
	world.next_staff_id += 1
	s.role = role
	s.sname = "%s %s" % [
		FIRST_NAMES[world.rng.randi_range_n(FIRST_NAMES.size())],
		LAST_NAMES[world.rng.randi_range_n(LAST_NAMES.size())],
	]
	s.base_nerve = NERVE_MIN + world.rng.randf01() * (NERVE_MAX - NERVE_MIN)
	s.shift = Staff.Shift.NIGHT if _count_role(world, role) % 2 == 1 else Staff.Shift.DAY
	s.state = Staff.State.OFF_DUTY
	s.place_at_tile(world.gate_tile)

	world.staff.append(s)
	world.events.emit("staff_hired", {"id": s.id, "role": role, "sname": s.sname, "fee": fee})
	return s


## Most recently hired staffer of a role, or null. What the fire key targets.
static func newest_of_role(world: SimWorld, role: int) -> Staff:
	for i in range(world.staff.size() - 1, -1, -1):
		if world.staff[i].role == role:
			return world.staff[i]
	return null


static func _count_role(world: SimWorld, role: int) -> int:
	var n := 0
	for s in world.staff:
		if s.role == role:
			n += 1
	return n
