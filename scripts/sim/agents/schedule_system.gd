class_name ScheduleSystem
extends RefCounted
## A 24-slot facility day. Per-facility and editable per the design doc;
## M2 ships one sensible default and the lookup, not an editor UI yet.

enum Block { SLEEP, EAT, WORK, YARD, FREE, SHOWER, LOCKUP, PROGRAM }

## Hour -> block, one entry per hour of the day.
const DEFAULT_SCHEDULE: Array[int] = [
	Block.SLEEP, Block.SLEEP, Block.SLEEP, Block.SLEEP, Block.SLEEP, Block.SLEEP, # 0-5
	Block.EAT, Block.WORK, Block.WORK, Block.WORK, Block.WORK, Block.EAT, # 6-11
	Block.YARD, Block.YARD, Block.PROGRAM, Block.PROGRAM, Block.EAT, Block.FREE, # 12-17
	Block.FREE, Block.SHOWER, Block.FREE, Block.LOCKUP, Block.LOCKUP, Block.SLEEP, # 18-23
]

var schedule: Array[int] = DEFAULT_SCHEDULE.duplicate()


func block_at_hour(hour: int) -> int:
	return schedule[clampi(hour, 0, 23)]
