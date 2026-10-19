# Do time — the prisoner mode (Claude, 2026-10-18)

The user's direction ("staff now, inmates next"): play as a prisoner, Hard Time style, in an ordinary facility that runs
itself. The management game stays staff-only; this is a separate start.

## Start

New holding → charter → on the last step, **OR DO TIME HERE AS A PRISONER: 7 / 14 / 30 days**. The facility is
chartered as usual (a normal company save), then `World.StartInmateRun(days)` hands the player one housed, ready resident
(moderate temperament, preferably unaffiliated), convicts their pending charges and sets the release date. Offered
only where residents can walk alone (not single-cell institutions, where secure movement needs an officer alongside).

## Playing

- Always a character view of your prisoner: in control when you can move, watching them when you cannot (locked in,
  cuffed, asleep, court). No management view; Esc / Start / D-pad ← / Menu opens the pause sheet (stats + journal,
  Back to it, Save and leave). Locked in: **Wait it out** runs time at 4×; asleep runs at 4× until 06:00.
- Same action pad as staff, a prisoner's words: **Punch** (Charge while running), **Shove**, **Pick up** (take a dropped
  item: it becomes yours, and searches find it), **Talk / Use** (talk to the person in front; at your bunk: Rest, or
  Sleep at night; at a shower or sink: Wash).
- Card: name, day N / total, health, respect, heat, where the day says you should be, and alerts.

## Rules (Core/InmateRun.cs)

- Fights: the person you hit may fight back (temperament, stress, gang, your respect) and keeps coming for ~25 minutes,
  swinging every few minutes. Dropping someone: +8 respect (+3 if they run with a gang). Being knocked out: −7 respect,
  down for 30 minutes.
- An on-shift officer with a clear line within 11 m who sees a fight walks over, cuffs you, walks you to your bunk and
  locks you in: fighting 6 h and 2 days added; hitting or shoving staff 24 h and 5 days (and every officer comes).
- Lights out: be within 2.5 m of your bunk at 22:30 or get written up and walked back (no lock-in).
- Low respect invites challenges: every 30 minutes in the day, someone nearby with a beef (tension ≥ 58) may come at you.
- Heat (staff attention) rises with fights, curfew and assaults and fades slowly. Release happens through the normal
  justice release; the run ends Released (or Escaped / Died if those systems end it).
- Saved: the run (sentence, stats, journal). Live only: fights in progress, an officer on the way, sleep.

## Hooks (exact exceptions logged in COORDINATION.md)

- Codex `CharacterControl.BeginCharacterControl`: residents may be controlled when `InmateMayControl(id)`.
- Codex `IncidentScenes.TryIncidentStaffTask`: `TryInmateStaffTask(staff)` moves the responding officer.
- Codex `GameCharacterControl.StartCharacterPlay`: the same allowance at runtime entry.
- Claude files: StaffActions (pad dispatch, speed, minute), Population (hydrate, movement), FacilityActionView (beats by
  any actor, a talk gesture), GameController/GameHud/GameModesHud/GameLookInput/GameActionPad/GamePressureHud.

## Checks

- Core: `Tools/CoreChecks/InmateChecks.cs` (10 tests).
- Unity: `Editor/InmateCapture.cs` → Builds/InmateReview/ (charter row, start, punch, cuffed, walked to the bunk and locked
  in, wait it out at 4×, menu, 900×420 touch, release).

## Not yet

Escape attempts by the player, a commissary/economy for prisoners, gangs recruiting you, character creation, and
measured phone performance.
