class_name Intake
extends RefCounted
## Prisoners arrive by bus and get assigned a cell. Generation is
## RNG-driven (world.rng) so the same seed always produces the same intake.

const FIRST_NAMES := [
	"James", "Robert", "Michael", "Marcus", "David", "Anthony", "Carlos", "Jamal",
	"Kevin", "Brian", "Eric", "Jason", "Willie", "Andre", "Darnell", "Victor",
	"Miguel", "Antoine", "Jerome", "Samuel",
]
const LAST_NAMES := [
	"Reyes", "Brooks", "Foster", "Coleman", "Hayes", "Mercer", "Dade", "Kowalski",
	"Nash", "Vance", "Ortiz", "Ibarra", "Whitfield", "Doyle", "Pruitt", "Lang",
	"Marsh", "Calder", "Rourke", "Voss",
]
const ALL_TRAITS := [
	Prisoner.Trait.VOLATILE, Prisoner.Trait.CUNNING, Prisoner.Trait.INSTITUTIONALIZED,
	Prisoner.Trait.FRAIL, Prisoner.Trait.CONNECTED, Prisoner.Trait.PENITENT,
]
const TRAIT_CHANCE := 0.18


static func generate_prisoner(world: SimWorld) -> Prisoner:
	var p := Prisoner.new()
	p.id = world.next_prisoner_id
	world.next_prisoner_id += 1
	p.pname = "%s %s" % [
		FIRST_NAMES[world.rng.randi_range_n(FIRST_NAMES.size())],
		LAST_NAMES[world.rng.randi_range_n(LAST_NAMES.size())],
	]
	p.age = world.rng.randi_between(19, 65)
	p.sentence_days = world.rng.randi_between(30, 3650)
	for t in ALL_TRAITS:
		if world.rng.chance(TRAIT_CHANCE):
			p.traits |= t
	return p


## Finds a free bed in a sealed, validly-zoned Cell, assigns it, and adds
## the prisoner to world.prisoners. False (nothing added) if no bed is free.
static func intake(world: SimWorld) -> bool:
	var bed := _find_free_bed(world)
	if bed == null:
		return false
	var p := generate_prisoner(world)
	p.cell_bed_pos = Vector2i(bed.x, bed.y)
	bed.owner_id = p.id
	p.pos = Vector2(bed.x + 0.5, bed.y + 0.5)
	world.prisoners.append(p)
	world.events.emit("prisoner_intake", {"id": p.id, "pname": p.pname})
	return true


static func _find_free_bed(world: SimWorld) -> PlacedObject:
	for o in world.grid.objects:
		if o.object_type == ObjectDef.Type.BED and o.owner_id == -1:
			var room := world.room_at(o.x, o.y)
			if room != null and room.sealed and room.zone_kind == ZoneValidator.Kind.CELL:
				return o
	return null
