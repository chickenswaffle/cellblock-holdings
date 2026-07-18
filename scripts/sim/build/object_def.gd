class_name ObjectDef
extends RefCounted
## Static registry of placeable object types. M1 keeps every object a single
## tile — multi-tile footprints are a later refinement, not needed yet.

enum Type {
	BED, TOILET, TABLE, BENCH, PHONE,
	WEIGHT_BENCH, SEWING_STATION, CCTV, METAL_DETECTOR,
}

const COST := {
	Type.BED: 150,
	Type.TOILET: 100,
	Type.TABLE: 60,
	Type.BENCH: 30,
	Type.PHONE: 80,
	Type.WEIGHT_BENCH: 200,
	Type.SEWING_STATION: 250,
	Type.CCTV: 120,
	Type.METAL_DETECTOR: 300,
}


static func cost_of(object_type: int) -> int:
	return COST.get(object_type, 0)
