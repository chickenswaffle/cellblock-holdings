# Yard life

Claude, 2026-10-18 (the user: "all the prisoners just stand around like zombies"). Yard time (10–12 and 15–17, shifted by each resident's routine offset) used to send everyone to a fixed spot in a 3-wide grid, where they stood still for four hours a day. Now each resident picks something to do for every 40-minute block, weighted by personality.

| Activity | Who leans toward it | What it looks like | Effect |
| --- | --- | --- | --- |
| Laps | disciplined | walks a loop around the yard edge in their own direction | a little stress relief |
| Workout: push-ups, sit-ups, jumping jacks, shadow boxing | impulsive, gang members, healthy | at their usual yard spot | counts as exercise minutes, stress relief |
| Chat circle | sociable | fellow gang members, or the unaffiliated, stand around one centre facing in; the speaker gestures and the turn passes round | belonging |
| Lean on the fence | loners | back to the nearest yard edge, arms folded, one foot up | none |
| Sit on the ground | unwell or tired | at the nearest yard edge, arms over the knees | none |
| Pace | stressed | back and forth across their spot | a little stress relief |

## Rules

- **Determinism.** The choice is a pure function of the clock, the day and the person (seeded hashes), so nothing is saved and a reload continues exactly. Re-routing compares the target with the saved movement destination, never with a runtime memory.
- **Priority.** Meal travel, washing, jobs, quarantine and separation, incidents, staff holds and knockdowns all keep priority: they decide the target after the yard hook, or the yard hook is never reached.
- **Pace.** People cover about 19 m an hour, so blocks are 40 minutes and edge spots are taken from the yard edge nearest each person's usual spot. Everyone reaches their activity and spends most of the block at it.
- **Circles.** Nobody stands in a circle of one: a crowd needs at least two people choosing it that block.
- **Hook.** `YardTarget` (declared in `Simulation.MoveResidents`) is implemented in `Core/YardLife.cs`. Poses are in `Runtime/FacilityYardView.cs`, called from the escape-scene pass with the lowest priority (any other scene that holds the body wins).

## Evidence

- **Core:** 4 checks (`Tools/CoreChecks/YardLifeChecks.cs`): at least four activities and real movement, reload continuity, chat circles seated around one centre, and washing/meal priority with workouts counting as exercise.
- **Side effect:** yard movement turns some fights into confrontations an officer breaks up (fights 13 → 7, "Threatening behavior" write-ups 4 → 12 over six days in the game-modes fixture). That check's fixture now keeps tensions low while settling, because it is about the type's good-time credit.
- **Unity** (`Editor/YardCapture.cs`, Claude's clone, batchmode): a state-medium company at 10:52 with 8 of 12 residents busy (laps, leaning, shadow boxing, push-ups, pacing) and no runtime errors. Frames: the wide yard, a workout close-up, the fence.
