# Staff actions: Hard Time-style controls for officers

Claude, 2026-10-18. The user asked for controls inspired by MDickie's Hard Time III ("you have controls where you can fight, interact, pick stuff up"), and chose **staff now, a prisoner mode later**. When you play as a staff member (direct first or third person), an action pad sits beside the look stick on touch screens, or in the corner with key labels on desktop.

| Button | Key | What it does |
| --- | --- | --- |
| **STRIKE** | F | Hit the resident in front. Running, it becomes a **charge** (a harder hit that always knocks down). While holding someone, it is a **takedown**. |
| **GRAB** | G | Take hold of the resident in front. Again: **cuff** (always works on someone on the floor or not resisting; otherwise a struggle you can lose). Again while cuffed: **release**. |
| **PICK UP** | Q | Pick up a dropped package (drone drops, throw-overs) within reach. It is confiscated before a resident can collect it. |
| **USE** | E | **Pat search** the resident in front, **search a bunk** within reach, or **lock in** a cuffed resident at their own bunk (six hours confined). With nothing physical to do, it falls back to the ordinary interaction (duty brief, phone). |
| **RUN** | hold Shift (or tap RUN) | 65% faster while the officer has wind (it drains 2 a minute; standing recovers 1.5 a minute). Escorting someone walks at 60%. |

The pad also shows the target ("TARGET · NAME", "HOLDING · NAME · RESISTING", "ESCORTING · NAME"), the officer's wind, and the result of the last action. An amber ring marks the person the pad would act on (blue while holding them).

## Consequences

- **Rulings.** Force is **justified** against a resident who is resisting, is holding a weapon, or has just been found with contraband. Otherwise it is **excessive**. Excessive force means:
  - a staff write-up ("Excessive force", severity 2, or 3 for a charge) and −2 integrity;
  - the resident's stress +10 and safety −15, and staff lose 3 trust (the tip line and informants feel it);
  - a memory ("hit me for nothing");
  - an inspection finding under **Conduct** (−3 each, up to −12).
- **Hits.** About 72% land (better with experience, worse against disciplined residents, near-certain on someone already down). A hit costs 8 health (14 for a charge). Some knock the resident down for 5–8 minutes.
- **Counters.** Impulsive or wound-up residents may **hit back**: the officer staggers for two minutes, the resident is filed for assault on staff and becomes *resisting* (force is now justified).
- **Grabbing.** People with harmful contraband on them, or short fuses, resist the hold. A resisting resident may **break free** and shove the officer back.
- **Escorting.** A cuffed resident walks the officer's own path a step behind, so they never cut a corner through a wall.
- **Lock-in.** Locking someone in uses the existing "kept in housing" restriction, honoured by routines, jobs and fights. It is **arbitrary** without resistance or contraband found that shift: a write-up, stress, a little trust, and a Conduct finding (−2 each, up to −6).
- **Searches.** Searches find each item with 60% (pat) or 50% (bunk) chance, +15% per search-equipment tier, +1% per year of experience. A find seizes the item, writes the resident up and justifies restraint or confinement for the rest of the shift. A clean pat search costs a sliver of trust.
- **Exclusions.** Residents owned by a fight scene, an escape or its struggle, intake or transport are not targets: those systems keep ownership. Walking up to a fight still intervenes through the existing incident rules.
- **What is saved.** Holds, knockdowns and wind are live state, like the control session: a reload hands everyone back to the simulation. Counters and the force log (24 entries) are saved.

## Evidence

- **Core:** 12 checks in `Tools/CoreChecks/StaffActionChecks.cs`. They cover migration, control and pause rules, excessive versus justified force with pinned trust and audit points, grab, cuff and the walk to the bunk through real doors with lock-in, takedown and certain cuffing, pat search, bunk search, confiscating a drone drop, the run and escort speed ratios with wind, escape exclusion, and counters.
- **Unity** (`Editor/ActionCapture.cs`, Claude's clone, batchmode with graphics): play as an officer in a disposable county company, then strike, grab, cuff and escort. It checks the pad's target and labels, that the cuffed resident walks 0.55 m behind, that the pad fits with buttons of at least 44 px, and that there are no runtime errors (`CELLBLOCK_ACTION_CAPTURE_PASSED`). Six composited frames.

## Files

- New (Claude): `Core/StaffActions.cs`, `Runtime/GameActionPad.cs`, `Runtime/GameActionInput.cs`, `Runtime/FacilityActionView.cs`, `Resources/ActionPad.uss`, `Editor/ActionCapture.cs`, `Tools/CoreChecks/StaffActionChecks.cs`, with metas.
- Exact hooks in Codex's character-experience files (claimed for this pass):
  - `CharacterControl.cs`: one `StaffActionSpeed(ref speed)` call plus `if (speed <= 0) return;` in `AdvanceCharacterControl`.
  - `GameCharacterControlsHud.cs`: `BuildActionPad()`, `RefreshActionPad(world)`, `LayoutActionPad(...)` with the context-line margin, and `ActionPadOwns(p)` in `CharacterInputPointOwned`.
  - `GameCharacterControl.cs`: one `ActionKeys()` call before the E interaction.
- Hooks in Claude's files: `Simulation.cs` (`AdvanceStaffActions` after `AdvanceCharacterControl`), `Population.cs` (hydrate, `StaffActionMovement` in `PressureMovement`, Conduct findings and snapshot), `FacilityEscapeView.cs` (`RefreshStaffActionScene` before the Keep loop).

## Next

- A separate prisoner mode reusing the same pad.
- Use of held props as weapons (baton, chair).
- Officer injuries.
- A radio button that runs the real 12-minute call.
- A shove move.
- Locking doors once door leaves exist.
